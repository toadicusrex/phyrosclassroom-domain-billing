namespace PhyrosClassroom.Billing.Models;

public sealed record BillingOperationsSummary(
    int LedgerCount,
    int OpenInvoiceCount,
    int OverdueInvoiceCount,
    decimal TotalOutstandingBalance,
    int AutoPayEnabledLedgerCount,
    int AutoPayEligibleInvoiceCount,
    int FailedChargeAttemptCount,
    int UnresolvedChargeAttemptCount,
    DateOnly AsOfDate);

public sealed record BillingOverdueInvoiceSummary(
    Guid RegistrationId,
    string SubjectId,
    string HouseholdName,
    Guid InvoiceId,
    string InvoiceNumber,
    DateOnly DueDate,
    decimal BalanceDue,
    bool AutoPayRequested,
    string? DefaultPaymentMethodLabel,
    string? LatestChargeStatus,
    string? LatestChargeFailureReason,
    DateTimeOffset UpdatedAtUtc);
