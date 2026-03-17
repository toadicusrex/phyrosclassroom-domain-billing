using PhyrosClassroom.Billing.Engines;
using PhyrosClassroom.Billing.Infrastructure.Persistence;
using PhyrosClassroom.Billing.Models;
using PhyrosClassroom.Billing.Orchestration;

namespace PhyrosClassroom.Billing.Orchestration.Default;

public sealed class InitializeBillingLedgerUseCase(IBillingLedgerStore store) : IInitializeBillingLedgerUseCase
{
    public async Task<BillingLedger> ExecuteAsync(InitializeBillingLedgerRequest request, CancellationToken cancellationToken = default)
    {
        var existing = await store.GetByRegistrationIdAsync(request.RegistrationId, cancellationToken);
        return existing ?? await store.SaveAsync(BillingLedgerFactory.Initialize(request), cancellationToken);
    }
}

public sealed class GetBillingLedgerByRegistrationIdUseCase(IBillingLedgerStore store) : IGetBillingLedgerByRegistrationIdUseCase
{
    public Task<BillingLedger?> ExecuteAsync(Guid registrationId, CancellationToken cancellationToken = default) =>
        store.GetByRegistrationIdAsync(registrationId, cancellationToken);
}

public sealed class GetBillingLedgerBySubjectIdUseCase(IBillingLedgerStore store) : IGetBillingLedgerBySubjectIdUseCase
{
    public Task<BillingLedger?> ExecuteAsync(string subjectId, CancellationToken cancellationToken = default) =>
        store.GetBySubjectIdAsync(subjectId, cancellationToken);
}

public sealed class CreateBillingInvoiceUseCase(IBillingLedgerStore store) : ICreateBillingInvoiceUseCase
{
    public async Task<BillingLedger> ExecuteAsync(CreateBillingInvoiceRequest request, CancellationToken cancellationToken = default)
    {
        var ledger = await store.GetByRegistrationIdAsync(request.RegistrationId, cancellationToken)
            ?? throw new InvalidOperationException("No billing ledger exists for this registration.");

        if (request.Lines.Count == 0 || request.Lines.Any(line => string.IsNullOrWhiteSpace(line.Description) || line.Quantity <= 0 || line.UnitAmount < 0m))
        {
            throw new InvalidOperationException("Invoice lines must include a description, positive quantity, and non-negative unit amount.");
        }

        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var existingInvoice = ledger.Invoices.FirstOrDefault(invoice =>
                string.Equals(invoice.CreatedFromKey, request.IdempotencyKey.Trim(), StringComparison.Ordinal));
            if (existingInvoice is not null)
            {
                return ledger;
            }
        }

        var invoice = BillingLedgerFactory.CreateInvoice(request, ledger);
        var updatedLedger = ledger with
        {
            Invoices = ledger.Invoices.Concat([invoice]).OrderByDescending(existing => existing.CreatedAtUtc).ToArray(),
            UpdatedAtUtc = invoice.UpdatedAtUtc,
        };

        return await store.SaveAsync(updatedLedger, cancellationToken);
    }
}

public sealed class RecordBillingPaymentUseCase(IBillingLedgerStore store) : IRecordBillingPaymentUseCase
{
    public async Task<BillingLedger> ExecuteAsync(RecordBillingPaymentRequest request, CancellationToken cancellationToken = default)
    {
        var ledger = await store.GetByRegistrationIdAsync(request.RegistrationId, cancellationToken)
            ?? throw new InvalidOperationException("No billing ledger exists for this registration.");
        var invoice = ledger.Invoices.FirstOrDefault(existing => existing.InvoiceId == request.InvoiceId)
            ?? throw new InvalidOperationException("Invoice was not found.");

        if (request.Amount <= 0m)
        {
            throw new InvalidOperationException("Payment amount must be greater than zero.");
        }

        var normalizedReference = request.Reference.Trim();
        if (string.IsNullOrWhiteSpace(request.Method) || string.IsNullOrWhiteSpace(normalizedReference))
        {
            throw new InvalidOperationException("Payment method and reference are required.");
        }

        if (invoice.Payments.Any(existing => string.Equals(existing.Reference, normalizedReference, StringComparison.Ordinal)))
        {
            return ledger;
        }

        var updatedInvoice = BillingLedgerFactory.ApplyPayment(invoice, request with { Reference = normalizedReference });
        var updatedLedger = ledger with
        {
            Invoices = ledger.Invoices.Select(existing => existing.InvoiceId == invoice.InvoiceId ? updatedInvoice : existing).ToArray(),
            UpdatedAtUtc = updatedInvoice.UpdatedAtUtc,
        };

        return await store.SaveAsync(updatedLedger, cancellationToken);
    }
}

public sealed class ChargeBillingInvoiceUseCase(
    IBillingLedgerStore store,
    IExternalBillingGateway externalBillingGateway) : IChargeBillingInvoiceUseCase
{
    public async Task<BillingLedger> ExecuteAsync(ChargeBillingInvoiceRequest request, CancellationToken cancellationToken = default)
    {
        var ledger = await store.GetByRegistrationIdAsync(request.RegistrationId, cancellationToken)
            ?? throw new InvalidOperationException("No billing ledger exists for this registration.");
        var invoice = ledger.Invoices.FirstOrDefault(existing => existing.InvoiceId == request.InvoiceId)
            ?? throw new InvalidOperationException("Invoice was not found.");

        var idempotencyKey = request.IdempotencyKey.Trim();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new InvalidOperationException("Charge idempotency key is required.");
        }

        var chargeAttempts = (invoice.ChargeAttempts ?? []).ToArray();
        if (chargeAttempts.Any(existing => string.Equals(existing.IdempotencyKey, idempotencyKey, StringComparison.Ordinal)))
        {
            return ledger;
        }

        if (invoice.BalanceDue <= 0m)
        {
            throw new InvalidOperationException("Invoice does not have an open balance.");
        }

        var amountToCharge = request.Amount ?? invoice.BalanceDue;
        if (amountToCharge <= 0m || amountToCharge > invoice.BalanceDue)
        {
            throw new InvalidOperationException("Charge amount must be greater than zero and no more than the current invoice balance.");
        }

        var gatewayResult = await externalBillingGateway.ChargeInvoiceAsync(
            ledger,
            invoice,
            new ExternalBillingChargeRequest(
                request.RegistrationId,
                request.InvoiceId,
                amountToCharge,
                idempotencyKey,
                request.RequestedByUserId,
                request.Notes),
            cancellationToken);

        var chargeAttempt = new BillingChargeAttempt(
            Guid.NewGuid(),
            idempotencyKey,
            amountToCharge,
            gatewayResult.ProcessorName,
            gatewayResult.ResultStatus,
            gatewayResult.ExternalReference,
            gatewayResult.FailureReason,
            DateTimeOffset.UtcNow,
            request.RequestedByUserId);

        var updatedInvoice = invoice with
        {
            ChargeAttempts = chargeAttempts.Concat([chargeAttempt]).ToArray(),
            UpdatedAtUtc = chargeAttempt.AttemptedAtUtc,
        };

        if (gatewayResult.Succeeded)
        {
            updatedInvoice = BillingLedgerFactory.ApplyPayment(
                updatedInvoice,
                new RecordBillingPaymentRequest(
                    request.RegistrationId,
                    request.InvoiceId,
                    amountToCharge,
                    $"Processor:{gatewayResult.ProcessorName}",
                    gatewayResult.ExternalReference ?? idempotencyKey,
                    request.Notes,
                    request.RequestedByUserId));
        }

        var updatedLedger = ledger with
        {
            Invoices = ledger.Invoices.Select(existing => existing.InvoiceId == invoice.InvoiceId ? updatedInvoice : existing).ToArray(),
            UpdatedAtUtc = updatedInvoice.UpdatedAtUtc,
        };

        return await store.SaveAsync(updatedLedger, cancellationToken);
    }
}

public sealed class UpdateBillingPaymentPlanUseCase(IBillingLedgerStore store) : IUpdateBillingPaymentPlanUseCase
{
    public async Task<BillingLedger> ExecuteAsync(UpdateBillingPaymentPlanRequest request, CancellationToken cancellationToken = default)
    {
        var ledger = await store.GetByRegistrationIdAsync(request.RegistrationId, cancellationToken)
            ?? throw new InvalidOperationException("No billing ledger exists for this registration.");

        var normalizedDay = request.RequestedChargeDayOfMonth;
        if (normalizedDay is < 1 or > 28)
        {
            throw new InvalidOperationException("Requested charge day must be between 1 and 28.");
        }

        var updatedAtUtc = DateTimeOffset.UtcNow;
        var updatedLedger = ledger with
        {
            PaymentPlan = ledger.PaymentPlan with
            {
                AutoPayRequested = request.AutoPayRequested,
                RequestedChargeDayOfMonth = request.AutoPayRequested ? normalizedDay : null,
                DefaultPaymentMethodLabel = string.IsNullOrWhiteSpace(request.DefaultPaymentMethodLabel)
                    ? null
                    : request.DefaultPaymentMethodLabel.Trim(),
                UpdatedAtUtc = updatedAtUtc,
                UpdatedByUserId = request.UpdatedByUserId,
            },
            UpdatedAtUtc = updatedAtUtc,
        };

        return await store.SaveAsync(updatedLedger, cancellationToken);
    }
}

public sealed class UpsertBillingPaymentMethodUseCase(IBillingLedgerStore store) : IUpsertBillingPaymentMethodUseCase
{
    public async Task<BillingLedger> ExecuteAsync(UpsertBillingPaymentMethodRequest request, CancellationToken cancellationToken = default)
    {
        var ledger = await store.GetByRegistrationIdAsync(request.RegistrationId, cancellationToken)
            ?? throw new InvalidOperationException("No billing ledger exists for this registration.");

        if (string.IsNullOrWhiteSpace(request.Label) || string.IsNullOrWhiteSpace(request.MethodKind) || string.IsNullOrWhiteSpace(request.MaskedDetails))
        {
            throw new InvalidOperationException("Payment method label, kind, and masked details are required.");
        }

        var updatedAtUtc = DateTimeOffset.UtcNow;
        var paymentMethodId = request.PaymentMethodId.GetValueOrDefault(Guid.NewGuid());
        var paymentMethods = ledger.PaymentMethods
            .Where(existing => existing.PaymentMethodId != paymentMethodId)
            .Select(existing => existing with { IsDefault = request.IsDefault ? false : existing.IsDefault })
            .ToList();

        var paymentMethod = new BillingPaymentMethod(
            paymentMethodId,
            request.Label.Trim(),
            request.MethodKind.Trim(),
            request.MaskedDetails.Trim(),
            string.IsNullOrWhiteSpace(request.ProviderName) ? null : request.ProviderName.Trim(),
            string.IsNullOrWhiteSpace(request.ExternalCustomerId) ? null : request.ExternalCustomerId.Trim(),
            string.IsNullOrWhiteSpace(request.ExternalPaymentMethodId) ? null : request.ExternalPaymentMethodId.Trim(),
            request.IsDefault,
            updatedAtUtc,
            request.UpdatedByUserId);

        paymentMethods.Add(paymentMethod);

        var currentDefaultLabel = request.IsDefault
            ? paymentMethod.Label
            : paymentMethods.FirstOrDefault(existing => existing.IsDefault)?.Label ?? ledger.PaymentPlan.DefaultPaymentMethodLabel;

        var updatedLedger = ledger with
        {
            PaymentMethods = paymentMethods.OrderBy(existing => existing.Label).ToArray(),
            PaymentPlan = ledger.PaymentPlan with
            {
                DefaultPaymentMethodLabel = currentDefaultLabel,
                UpdatedAtUtc = updatedAtUtc,
                UpdatedByUserId = request.UpdatedByUserId,
            },
            UpdatedAtUtc = updatedAtUtc,
        };

        return await store.SaveAsync(updatedLedger, cancellationToken);
    }
}

public sealed class RunBillingAutoPayBatchUseCase(
    IBillingLedgerStore store,
    IChargeBillingInvoiceUseCase chargeBillingInvoiceUseCase) : IRunBillingAutoPayBatchUseCase
{
    public async Task<BillingAutoPayRunResult> ExecuteAsync(RunBillingAutoPayBatchRequest request, CancellationToken cancellationToken = default)
    {
        var idempotencyKey = request.IdempotencyKey.Trim();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new InvalidOperationException("Auto-pay batch idempotency key is required.");
        }

        if (string.IsNullOrWhiteSpace(request.RequestedByUserId))
        {
            throw new InvalidOperationException("Requested by user id is required.");
        }

        var ledgers = await store.ListAsync(cancellationToken);
        var eligibleInvoices = ledgers
            .SelectMany(ledger => ledger.Invoices
                .Where(invoice => IsEligibleForAutoPay(ledger, invoice, request.RunDate))
                .Select(invoice => new { Ledger = ledger, Invoice = invoice }))
            .OrderBy(item => item.Ledger.HouseholdName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Invoice.DueDate)
            .ToList();

        var results = new List<BillingAutoPayRunInvoiceResult>(eligibleInvoices.Count);

        foreach (var item in eligibleInvoices)
        {
            var paymentMethodLabel = item.Ledger.PaymentPlan.DefaultPaymentMethodLabel;
            if (request.DryRun)
            {
                results.Add(new BillingAutoPayRunInvoiceResult(
                    item.Ledger.RegistrationId,
                    item.Invoice.InvoiceId,
                    item.Ledger.SubjectId,
                    item.Ledger.HouseholdName,
                    item.Invoice.InvoiceNumber,
                    item.Invoice.BalanceDue,
                    "DryRunEligible",
                    "DryRun",
                    null,
                    null,
                    paymentMethodLabel));
                continue;
            }

            var attemptKey = $"{idempotencyKey}:{item.Ledger.RegistrationId:N}:{item.Invoice.InvoiceId:N}:{request.RunDate:yyyyMMdd}";
            var updatedLedger = await chargeBillingInvoiceUseCase.ExecuteAsync(
                new ChargeBillingInvoiceRequest(
                    item.Ledger.RegistrationId,
                    item.Invoice.InvoiceId,
                    item.Invoice.BalanceDue,
                    attemptKey,
                    request.RequestedByUserId,
                    $"AutoPay batch {request.RunDate:yyyy-MM-dd}"),
                cancellationToken);

            var updatedInvoice = updatedLedger.Invoices.First(updated => updated.InvoiceId == item.Invoice.InvoiceId);
            var latestAttempt = (updatedInvoice.ChargeAttempts ?? []).FirstOrDefault(existing =>
                string.Equals(existing.IdempotencyKey, attemptKey, StringComparison.Ordinal));

            results.Add(new BillingAutoPayRunInvoiceResult(
                updatedLedger.RegistrationId,
                updatedInvoice.InvoiceId,
                updatedLedger.SubjectId,
                updatedLedger.HouseholdName,
                updatedInvoice.InvoiceNumber,
                latestAttempt?.Amount ?? item.Invoice.BalanceDue,
                latestAttempt?.ResultStatus ?? updatedInvoice.Status,
                latestAttempt?.ProcessorName ?? "Unknown",
                latestAttempt?.ExternalReference,
                latestAttempt?.FailureReason,
                paymentMethodLabel));
        }

        return new BillingAutoPayRunResult(
            request.RunDate,
            idempotencyKey,
            request.DryRun,
            eligibleInvoices.Count,
            results.Count(result => !string.Equals(result.ResultStatus, "DryRunEligible", StringComparison.Ordinal)),
            results,
            DateTimeOffset.UtcNow);
    }

    private static bool IsEligibleForAutoPay(BillingLedger ledger, BillingInvoice invoice, DateOnly runDate) =>
        BillingEligibility.IsEligibleForAutoPay(ledger, invoice, runDate);
}

public sealed class ResolveBillingChargeAttemptUseCase(IBillingLedgerStore store) : IResolveBillingChargeAttemptUseCase
{
    public async Task<BillingLedger> ExecuteAsync(ResolveBillingChargeAttemptRequest request, CancellationToken cancellationToken = default)
    {
        var ledger = await store.GetByRegistrationIdAsync(request.RegistrationId, cancellationToken)
            ?? throw new InvalidOperationException("No billing ledger exists for this registration.");
        var invoice = ledger.Invoices.FirstOrDefault(existing => existing.InvoiceId == request.InvoiceId)
            ?? throw new InvalidOperationException("Invoice was not found.");
        var chargeAttempt = (invoice.ChargeAttempts ?? []).FirstOrDefault(existing => existing.ChargeAttemptId == request.ChargeAttemptId)
            ?? throw new InvalidOperationException("Charge attempt was not found.");

        if (string.IsNullOrWhiteSpace(request.ResolutionStatus))
        {
            throw new InvalidOperationException("Resolution status is required.");
        }

        var resolvedAtUtc = DateTimeOffset.UtcNow;
        var updatedAttempt = chargeAttempt with
        {
            ResolutionStatus = request.ResolutionStatus.Trim(),
            ResolutionNotes = string.IsNullOrWhiteSpace(request.ResolutionNotes) ? null : request.ResolutionNotes.Trim(),
            ResolvedAtUtc = resolvedAtUtc,
            ResolvedByUserId = request.ResolvedByUserId,
        };

        var updatedInvoice = invoice with
        {
            ChargeAttempts = (invoice.ChargeAttempts ?? [])
                .Select(existing => existing.ChargeAttemptId == request.ChargeAttemptId ? updatedAttempt : existing)
                .ToArray(),
            UpdatedAtUtc = resolvedAtUtc,
        };

        var updatedLedger = ledger with
        {
            Invoices = ledger.Invoices.Select(existing => existing.InvoiceId == invoice.InvoiceId ? updatedInvoice : existing).ToArray(),
            UpdatedAtUtc = resolvedAtUtc,
        };

        return await store.SaveAsync(updatedLedger, cancellationToken);
    }
}

public sealed class GetBillingOperationsSummaryUseCase(IBillingLedgerStore store) : IGetBillingOperationsSummaryUseCase
{
    public async Task<BillingOperationsSummary> ExecuteAsync(DateOnly? asOfDate = null, CancellationToken cancellationToken = default)
    {
        var effectiveDate = asOfDate ?? DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var ledgers = await store.ListAsync(cancellationToken);
        var invoices = ledgers.SelectMany(ledger => ledger.Invoices.Select(invoice => new { Ledger = ledger, Invoice = invoice })).ToArray();
        var overdueInvoices = invoices.Where(item => item.Invoice.BalanceDue > 0m && item.Invoice.DueDate < effectiveDate).ToArray();

        return new BillingOperationsSummary(
            ledgers.Count,
            invoices.Count(item => item.Invoice.BalanceDue > 0m),
            overdueInvoices.Length,
            invoices.Sum(item => item.Invoice.BalanceDue),
            ledgers.Count(ledger => ledger.PaymentPlan.AutoPayRequested),
            invoices.Count(item => BillingEligibility.IsEligibleForAutoPay(item.Ledger, item.Invoice, effectiveDate)),
            invoices.Sum(item => item.Invoice.ChargeAttempts?.Count(attempt =>
                    string.Equals(attempt.ResultStatus, "Declined", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(attempt.ResultStatus, "Blocked", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(attempt.ResultStatus, "Failed", StringComparison.OrdinalIgnoreCase)) ?? 0),
            invoices.Sum(item => item.Invoice.ChargeAttempts?.Count(attempt =>
                    BillingChargeReviewPolicy.RequiresReview(attempt) &&
                    string.IsNullOrWhiteSpace(attempt.ResolutionStatus)) ?? 0),
            effectiveDate);
    }
}

public sealed class ListBillingOverdueInvoicesUseCase(IBillingLedgerStore store) : IListBillingOverdueInvoicesUseCase
{
    public async Task<IReadOnlyList<BillingOverdueInvoiceSummary>> ExecuteAsync(DateOnly? asOfDate = null, CancellationToken cancellationToken = default)
    {
        var effectiveDate = asOfDate ?? DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var ledgers = await store.ListAsync(cancellationToken);

        return ledgers
            .SelectMany(ledger => ledger.Invoices
                .Where(invoice => invoice.BalanceDue > 0m && invoice.DueDate < effectiveDate)
                .Select(invoice =>
                {
                    var latestAttempt = invoice.ChargeAttempts?.OrderByDescending(attempt => attempt.AttemptedAtUtc).FirstOrDefault();
                    return new BillingOverdueInvoiceSummary(
                        ledger.RegistrationId,
                        ledger.SubjectId,
                        ledger.HouseholdName,
                        invoice.InvoiceId,
                        invoice.InvoiceNumber,
                        invoice.DueDate,
                        invoice.BalanceDue,
                        ledger.PaymentPlan.AutoPayRequested,
                        ledger.PaymentPlan.DefaultPaymentMethodLabel,
                        latestAttempt?.ResultStatus,
                        latestAttempt?.FailureReason,
                        invoice.UpdatedAtUtc);
                }))
            .OrderBy(item => item.DueDate)
            .ThenBy(item => item.HouseholdName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}

public sealed class ListBillingChargeReviewQueueUseCase(IBillingLedgerStore store) : IListBillingChargeReviewQueueUseCase
{
    public async Task<IReadOnlyList<BillingChargeReviewItem>> ExecuteAsync(bool includeResolved = false, CancellationToken cancellationToken = default)
    {
        var ledgers = await store.ListAsync(cancellationToken);

        return ledgers
            .SelectMany(ledger => ledger.Invoices.SelectMany(invoice => (invoice.ChargeAttempts ?? [])
                .Where(BillingChargeReviewPolicy.RequiresReview)
                .Where(attempt => includeResolved || string.IsNullOrWhiteSpace(attempt.ResolutionStatus))
                .Select(attempt => new BillingChargeReviewItem(
                    ledger.RegistrationId,
                    invoice.InvoiceId,
                    attempt.ChargeAttemptId,
                    ledger.SubjectId,
                    ledger.HouseholdName,
                    invoice.InvoiceNumber,
                    attempt.Amount,
                    attempt.ProcessorName,
                    attempt.ResultStatus,
                    attempt.ExternalReference,
                    attempt.FailureReason,
                    invoice.DueDate,
                    attempt.AttemptedAtUtc,
                    attempt.AttemptedByUserId,
                    !string.IsNullOrWhiteSpace(attempt.ResolutionStatus),
                    attempt.ResolutionStatus,
                    attempt.ResolutionNotes,
                    attempt.ResolvedAtUtc,
                    attempt.ResolvedByUserId))))
            .OrderByDescending(item => item.AttemptedAtUtc)
            .ThenBy(item => item.HouseholdName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}

public sealed class GetBillingReconciliationSummaryUseCase(IBillingLedgerStore store) : IGetBillingReconciliationSummaryUseCase
{
    public async Task<BillingReconciliationSummary> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var ledgers = await store.ListAsync(cancellationToken);
        var invoices = ledgers.SelectMany(ledger => ledger.Invoices).ToArray();
        var attempts = invoices.SelectMany(invoice => invoice.ChargeAttempts ?? []).ToArray();

        var unresolvedInvoiceIds = invoices
            .Where(invoice => (invoice.ChargeAttempts ?? []).Any(attempt =>
                BillingChargeReviewPolicy.RequiresReview(attempt) &&
                string.IsNullOrWhiteSpace(attempt.ResolutionStatus)))
            .Select(invoice => invoice.InvoiceId)
            .ToHashSet();

        var resolvedInvoiceIds = invoices
            .Where(invoice => (invoice.ChargeAttempts ?? []).Any(attempt =>
                BillingChargeReviewPolicy.RequiresReview(attempt) &&
                !string.IsNullOrWhiteSpace(attempt.ResolutionStatus)))
            .Select(invoice => invoice.InvoiceId)
            .ToHashSet();

        return new BillingReconciliationSummary(
            attempts.Count(attempt => BillingChargeReviewPolicy.RequiresReview(attempt) && string.IsNullOrWhiteSpace(attempt.ResolutionStatus)),
            attempts.Count(attempt => BillingChargeReviewPolicy.RequiresReview(attempt) && !string.IsNullOrWhiteSpace(attempt.ResolutionStatus)),
            attempts.Count(attempt => string.Equals(attempt.ResultStatus, "Approved", StringComparison.OrdinalIgnoreCase) ||
                                      string.Equals(attempt.ResultStatus, "Succeeded", StringComparison.OrdinalIgnoreCase)),
            invoices.Where(invoice => resolvedInvoiceIds.Contains(invoice.InvoiceId)).Sum(invoice => invoice.BalanceDue),
            invoices.Where(invoice => unresolvedInvoiceIds.Contains(invoice.InvoiceId)).Sum(invoice => invoice.BalanceDue),
            DateTimeOffset.UtcNow);
    }
}

internal static class BillingEligibility
{
    public static bool IsEligibleForAutoPay(BillingLedger ledger, BillingInvoice invoice, DateOnly runDate)
    {
        if (!ledger.PaymentPlan.AutoPayRequested ||
            ledger.PaymentPlan.RequestedChargeDayOfMonth != runDate.Day ||
            string.IsNullOrWhiteSpace(ledger.PaymentPlan.DefaultPaymentMethodLabel) ||
            !ledger.PaymentMethods.Any(method => method.IsDefault || string.Equals(method.Label, ledger.PaymentPlan.DefaultPaymentMethodLabel, StringComparison.Ordinal)))
        {
            return false;
        }

        if (invoice.BalanceDue <= 0m || invoice.DueDate > runDate)
        {
            return false;
        }

        return true;
    }
}

internal static class BillingChargeReviewPolicy
{
    public static bool RequiresReview(BillingChargeAttempt attempt) =>
        string.Equals(attempt.ResultStatus, "Declined", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(attempt.ResultStatus, "Blocked", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(attempt.ResultStatus, "Failed", StringComparison.OrdinalIgnoreCase);
}
