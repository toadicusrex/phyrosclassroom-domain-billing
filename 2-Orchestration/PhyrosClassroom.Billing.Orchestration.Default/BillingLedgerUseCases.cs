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

    private static bool IsEligibleForAutoPay(BillingLedger ledger, BillingInvoice invoice, DateOnly runDate)
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
