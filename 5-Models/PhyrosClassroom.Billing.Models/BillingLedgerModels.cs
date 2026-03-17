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

public sealed record BillingChargeAttempt(
    Guid ChargeAttemptId,
    string IdempotencyKey,
    decimal Amount,
    string ProcessorName,
    string ResultStatus,
    string? ExternalReference,
    string? FailureReason,
    DateTimeOffset AttemptedAtUtc,
    string AttemptedByUserId,
    string? ResolutionStatus = null,
    string? ResolutionNotes = null,
    DateTimeOffset? ResolvedAtUtc = null,
    string? ResolvedByUserId = null);

public sealed record BillingPaymentMethod(
    Guid PaymentMethodId,
    string Label,
    string MethodKind,
    string MaskedDetails,
    string? ProviderName,
    string? ExternalCustomerId,
    string? ExternalPaymentMethodId,
    bool IsDefault,
    DateTimeOffset UpdatedAtUtc,
    string UpdatedByUserId);

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
    IReadOnlyList<BillingPayment> Payments,
    IReadOnlyList<BillingChargeAttempt>? ChargeAttempts = null);

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
    IReadOnlyList<BillingPaymentMethod> PaymentMethods,
    BillingPaymentPlan PaymentPlan,
    DateTimeOffset UpdatedAtUtc);

public sealed record BillingAutoPayRunInvoiceResult(
    Guid RegistrationId,
    Guid InvoiceId,
    string SubjectId,
    string HouseholdName,
    string InvoiceNumber,
    decimal Amount,
    string ResultStatus,
    string ProcessorName,
    string? ExternalReference,
    string? FailureReason,
    string? PaymentMethodLabel);

public sealed record BillingAutoPayRunResult(
    DateOnly RunDate,
    string IdempotencyKey,
    bool DryRun,
    int EligibleInvoiceCount,
    int ProcessedInvoiceCount,
    IReadOnlyList<BillingAutoPayRunInvoiceResult> Invoices,
    DateTimeOffset CompletedAtUtc);

public sealed record BillingChargeReviewItem(
    Guid RegistrationId,
    Guid InvoiceId,
    Guid ChargeAttemptId,
    string SubjectId,
    string HouseholdName,
    string InvoiceNumber,
    decimal Amount,
    string ProcessorName,
    string ResultStatus,
    string? ExternalReference,
    string? FailureReason,
    DateOnly DueDate,
    DateTimeOffset AttemptedAtUtc,
    string AttemptedByUserId,
    bool Resolved,
    string? ResolutionStatus,
    string? ResolutionNotes,
    DateTimeOffset? ResolvedAtUtc,
    string? ResolvedByUserId);
