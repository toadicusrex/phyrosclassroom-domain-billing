namespace PhyrosClassroom.Billing.Models;

public sealed record BillingReconciliationSummary(
    int UnresolvedChargeAttemptCount,
    int ResolvedChargeAttemptCount,
    int SuccessfulChargeAttemptCount,
    decimal OutstandingBalanceOnReviewedInvoices,
    decimal OutstandingBalanceOnUnreviewedInvoices,
    DateTimeOffset GeneratedAtUtc);
