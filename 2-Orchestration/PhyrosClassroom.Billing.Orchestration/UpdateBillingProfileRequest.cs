using PhyrosClassroom.Billing.Models;

namespace PhyrosClassroom.Billing.Orchestration;

public sealed record UpdateBillingProfileRequest(
    Guid BillingId,
    string BillingName,
    bool WantsEmailNotifications,
    bool WantsSmsNotifications,
    string? OnboardingStatus,
    IReadOnlyList<BillingContact> Contacts,
    IReadOnlyList<BillingStudent> Students,
    Guid? SourceRegistrationId,
    string? SourceRegistrationStatus,
    DateTimeOffset? SourceRegistrationUpdatedAtUtc,
    DateOnly? BirthDate,
    string? GradeLevel,
    string? PrimaryGuardianName,
    string? PrimaryGuardianEmail,
    bool HasMedicalAlert,
    string? MedicalNotes,
    bool HasIep,
    IReadOnlyList<BillingDocumentReference> Documents,
    IReadOnlyList<BillingRecordNote> Notes);
