using PhyrosClassroom.Billing.Models;

namespace PhyrosClassroom.Billing.Orchestration;

public sealed record RegisterBillingRequest(
    string GivenName,
    string FamilyName,
    string SubjectId = "",
    string BillingName = "",
    bool WantsEmailNotifications = false,
    bool WantsSmsNotifications = false,
    string? OnboardingStatus = null,
    IReadOnlyList<BillingContact>? Contacts = null,
    IReadOnlyList<BillingStudent>? Students = null,
    Guid? SourceRegistrationId = null,
    string? SourceRegistrationStatus = null,
    DateTimeOffset? SourceRegistrationUpdatedAtUtc = null,
    string? SourceSystem = null,
    string? SourceReference = null);
