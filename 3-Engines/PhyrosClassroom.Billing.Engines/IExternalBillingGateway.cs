using PhyrosClassroom.Billing.Models;

namespace PhyrosClassroom.Billing.Engines;

public sealed record ExternalBillingChargeRequest(
    Guid RegistrationId,
    Guid InvoiceId,
    decimal Amount,
    string IdempotencyKey,
    string RequestedByUserId,
    string? Notes);

public interface IExternalBillingGateway
{
    Task<ExternalBillingChargeResult> ChargeInvoiceAsync(
        BillingLedger ledger,
        BillingInvoice invoice,
        ExternalBillingChargeRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ExternalBillingChargeResult(
    bool Succeeded,
    string ProcessorName,
    string ResultStatus,
    string? ExternalReference,
    string? FailureReason);
