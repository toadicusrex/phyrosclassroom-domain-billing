using PhyrosClassroom.Billing.Models;

namespace PhyrosClassroom.Billing.Orchestration;

public sealed record InitializeBillingLedgerRequest(
    Guid RegistrationId,
    string SubjectId,
    string HouseholdName,
    string UpdatedByUserId);

public sealed record CreateBillingInvoiceRequest(
    Guid RegistrationId,
    string Description,
    DateOnly DueDate,
    IReadOnlyList<CreateBillingInvoiceLineItem> Lines,
    string CurrencyCode,
    string CreatedByUserId,
    string? IdempotencyKey);

public sealed record CreateBillingInvoiceLineItem(
    string Description,
    int Quantity,
    decimal UnitAmount);

public sealed record RecordBillingPaymentRequest(
    Guid RegistrationId,
    Guid InvoiceId,
    decimal Amount,
    string Method,
    string Reference,
    string? Notes,
    string RecordedByUserId);

public sealed record ChargeBillingInvoiceRequest(
    Guid RegistrationId,
    Guid InvoiceId,
    decimal? Amount,
    string IdempotencyKey,
    string RequestedByUserId,
    string? Notes);

public sealed record UpdateBillingPaymentPlanRequest(
    Guid RegistrationId,
    bool AutoPayRequested,
    int? RequestedChargeDayOfMonth,
    string? DefaultPaymentMethodLabel,
    string UpdatedByUserId);
