namespace PhyrosClassroom.Billing.Models;

public sealed record BillingContact(
    string FullName,
    string RelationshipToChildren,
    string Email,
    string Phone,
    bool IsPrimaryContact,
    bool WantsPortalAccess);

public sealed record BillingStudent(
    Guid? StudentId,
    string StudentCode,
    string GivenName,
    string FamilyName,
    string GradeLevel,
    DateOnly? BirthDate,
    string RelationshipToPrimaryContact);

public sealed record BillingDocumentReference(
    string DocumentType,
    string FileName,
    DateTimeOffset UploadedAtUtc,
    string UploadedByUserId);

public sealed record BillingRecordNote(
    string Category,
    string Body,
    DateTimeOffset RecordedAtUtc,
    string RecordedByUserId);
