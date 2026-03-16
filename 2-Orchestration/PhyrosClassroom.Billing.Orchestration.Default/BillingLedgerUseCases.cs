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
