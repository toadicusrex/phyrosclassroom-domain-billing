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
