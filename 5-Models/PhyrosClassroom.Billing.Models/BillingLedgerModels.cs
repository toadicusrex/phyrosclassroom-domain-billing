namespace PhyrosClassroom.Billing.Models;

public sealed record BillingInvoiceLine(
    string Description,
    int Quantity,
    decimal UnitAmount,
    decimal LineTotal);

public sealed record BillingPayment(
    Guid PaymentId,
    decimal Amount,
    string Method,
    string Reference,
    string? Notes,
    DateTimeOffset RecordedAtUtc,
    string RecordedByUserId);

public sealed record BillingInvoice(
    Guid InvoiceId,
    string InvoiceNumber,
    string Status,
    string Description,
    DateOnly DueDate,
    IReadOnlyList<BillingInvoiceLine> Lines,
    decimal AmountDue,
    decimal AmountPaid,
    decimal BalanceDue,
    string CurrencyCode,
    string? CreatedFromKey,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<BillingPayment> Payments);

public sealed record BillingPaymentPlan(
    bool AutoPayRequested,
    int? RequestedChargeDayOfMonth,
    string? DefaultPaymentMethodLabel,
    DateTimeOffset UpdatedAtUtc,
    string UpdatedByUserId);

public sealed record BillingLedger(
    Guid LedgerId,
    Guid RegistrationId,
    string SubjectId,
    string HouseholdName,
    IReadOnlyList<BillingInvoice> Invoices,
    BillingPaymentPlan PaymentPlan,
    DateTimeOffset UpdatedAtUtc);
