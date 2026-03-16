namespace PhyrosClassroom.Billing.Models;

public sealed class BillingAggregate
{
    private readonly List<BillingEventRecord> _events = [];

    private BillingAggregate()
    {
    }

    public Guid BillingId { get; private set; }
    public string BillingCode { get; private set; } = string.Empty;
    public string SubjectId { get; private set; } = string.Empty;
    public Guid? SourceRegistrationId { get; private set; }
    public string? SourceRegistrationStatus { get; private set; }
    public DateTimeOffset? SourceRegistrationUpdatedAtUtc { get; private set; }
    public string BillingName { get; private set; } = string.Empty;
    public bool WantsEmailNotifications { get; private set; }
    public bool WantsSmsNotifications { get; private set; }
    public string? OnboardingStatus { get; private set; }
    public IReadOnlyList<BillingContact> Contacts { get; private set; } = [];
    public IReadOnlyList<BillingStudent> Students { get; private set; } = [];
    public string GivenName { get; private set; } = string.Empty;
    public string FamilyName { get; private set; } = string.Empty;
    public string? SourceSystem { get; private set; }
    public string? SourceReference { get; private set; }
    public DateOnly? BirthDate { get; private set; }
    public string? GradeLevel { get; private set; }
    public string? PrimaryGuardianName { get; private set; }
    public string? PrimaryGuardianEmail { get; private set; }
    public bool HasMedicalAlert { get; private set; }
    public string? MedicalNotes { get; private set; }
    public bool HasIep { get; private set; }
    public IReadOnlyList<BillingDocumentReference> Documents { get; private set; } = [];
    public IReadOnlyList<BillingRecordNote> Notes { get; private set; } = [];
    public DateTimeOffset RegisteredAtUtc { get; private set; }
    public IReadOnlyList<BillingEventRecord> Events => _events;

    public static BillingAggregate Register(
        Guid billingId,
        string billingCode,
        string givenName,
        string familyName,
        DateTimeOffset occurredUtc,
        string subjectId = "",
        string billingName = "",
        bool wantsEmailNotifications = false,
        bool wantsSmsNotifications = false,
        string? onboardingStatus = null,
        IReadOnlyList<BillingContact>? contacts = null,
        IReadOnlyList<BillingStudent>? students = null,
        Guid? sourceRegistrationId = null,
        string? sourceRegistrationStatus = null,
        DateTimeOffset? sourceRegistrationUpdatedAtUtc = null,
        string? sourceSystem = null,
        string? sourceReference = null)
    {
        var aggregate = new BillingAggregate();
        aggregate.Apply(
            new BillingEventRecord(
                billingId,
                "BillingRegistered",
                occurredUtc,
                billingCode,
                givenName.Trim(),
                familyName.Trim(),
                string.IsNullOrWhiteSpace(sourceSystem) ? null : sourceSystem.Trim(),
                string.IsNullOrWhiteSpace(sourceReference) ? null : sourceReference.Trim(),
                null,
                null,
                null,
                null,
                false,
                null,
                false,
                null,
                null,
                string.IsNullOrWhiteSpace(subjectId) ? string.Empty : subjectId.Trim(),
                sourceRegistrationId,
                string.IsNullOrWhiteSpace(sourceRegistrationStatus) ? null : sourceRegistrationStatus.Trim(),
                sourceRegistrationUpdatedAtUtc,
                string.IsNullOrWhiteSpace(billingName) ? string.Empty : billingName.Trim(),
                wantsEmailNotifications,
                wantsSmsNotifications,
                string.IsNullOrWhiteSpace(onboardingStatus) ? null : onboardingStatus.Trim(),
                contacts?.ToArray(),
                students?.ToArray()));

        return aggregate;
    }

    public static BillingAggregate? Rehydrate(IEnumerable<BillingEventRecord> eventHistory)
    {
        var aggregate = new BillingAggregate();

        foreach (var billingEvent in eventHistory.OrderBy(eventItem => eventItem.OccurredUtc))
        {
            aggregate.Apply(billingEvent);
        }

        return aggregate._events.Count == 0 ? null : aggregate;
    }

    public static BillingAggregate? RehydrateAt(
        IEnumerable<BillingEventRecord> eventHistory,
        DateTimeOffset pointInTimeUtc)
    {
        return Rehydrate(eventHistory.Where(eventItem => eventItem.OccurredUtc <= pointInTimeUtc));
    }

    public BillingReadModel ToReadModel()
    {
        return new BillingReadModel(
            BillingId,
            BillingCode,
            GivenName,
            FamilyName,
            RegisteredAtUtc,
            SourceSystem,
            SourceReference,
            BirthDate,
            GradeLevel,
            PrimaryGuardianName,
            PrimaryGuardianEmail,
            HasMedicalAlert,
            MedicalNotes,
            HasIep,
            Documents,
            Notes,
            SubjectId,
            SourceRegistrationId,
            SourceRegistrationStatus,
            SourceRegistrationUpdatedAtUtc,
            BillingName,
            WantsEmailNotifications,
            WantsSmsNotifications,
            OnboardingStatus,
            Contacts,
            Students,
            _events.LastOrDefault()?.OccurredUtc ?? RegisteredAtUtc);
    }

    public void UpdateProfile(
        string billingName,
        bool wantsEmailNotifications,
        bool wantsSmsNotifications,
        string? onboardingStatus,
        IReadOnlyList<BillingContact> contacts,
        IReadOnlyList<BillingStudent> students,
        Guid? sourceRegistrationId,
        string? sourceRegistrationStatus,
        DateTimeOffset? sourceRegistrationUpdatedAtUtc,
        DateOnly? birthDate,
        string? gradeLevel,
        string? primaryGuardianName,
        string? primaryGuardianEmail,
        bool hasMedicalAlert,
        string? medicalNotes,
        bool hasIep,
        IReadOnlyList<BillingDocumentReference> documents,
        IReadOnlyList<BillingRecordNote> notes,
        DateTimeOffset occurredUtc)
    {
        Apply(new BillingEventRecord(
            BillingId,
            "BillingProfileUpdated",
            occurredUtc,
            BillingCode,
            GivenName,
            FamilyName,
            SourceSystem,
            SourceReference,
            birthDate,
            string.IsNullOrWhiteSpace(gradeLevel) ? null : gradeLevel.Trim(),
            string.IsNullOrWhiteSpace(primaryGuardianName) ? null : primaryGuardianName.Trim(),
            string.IsNullOrWhiteSpace(primaryGuardianEmail) ? null : primaryGuardianEmail.Trim(),
            hasMedicalAlert,
            string.IsNullOrWhiteSpace(medicalNotes) ? null : medicalNotes.Trim(),
            hasIep,
            documents.ToArray(),
            notes.ToArray(),
            SubjectId,
            sourceRegistrationId,
            string.IsNullOrWhiteSpace(sourceRegistrationStatus) ? null : sourceRegistrationStatus.Trim(),
            sourceRegistrationUpdatedAtUtc,
            string.IsNullOrWhiteSpace(billingName) ? BillingName : billingName.Trim(),
            wantsEmailNotifications,
            wantsSmsNotifications,
            string.IsNullOrWhiteSpace(onboardingStatus) ? null : onboardingStatus.Trim(),
            contacts.ToArray(),
            students.ToArray()));
    }

    private void Apply(BillingEventRecord billingEvent)
    {
        BillingId = billingEvent.BillingId;
        BillingCode = billingEvent.BillingCode;
        SubjectId = billingEvent.SubjectId;
        SourceRegistrationId = billingEvent.SourceRegistrationId;
        SourceRegistrationStatus = billingEvent.SourceRegistrationStatus;
        SourceRegistrationUpdatedAtUtc = billingEvent.SourceRegistrationUpdatedAtUtc;
        BillingName = billingEvent.BillingName;
        WantsEmailNotifications = billingEvent.WantsEmailNotifications;
        WantsSmsNotifications = billingEvent.WantsSmsNotifications;
        OnboardingStatus = billingEvent.OnboardingStatus;
        Contacts = billingEvent.Contacts ?? [];
        Students = billingEvent.Students ?? [];
        GivenName = billingEvent.GivenName;
        FamilyName = billingEvent.FamilyName;
        SourceSystem = billingEvent.SourceSystem;
        SourceReference = billingEvent.SourceReference;
        BirthDate = billingEvent.BirthDate;
        GradeLevel = billingEvent.GradeLevel;
        PrimaryGuardianName = billingEvent.PrimaryGuardianName;
        PrimaryGuardianEmail = billingEvent.PrimaryGuardianEmail;
        HasMedicalAlert = billingEvent.HasMedicalAlert;
        MedicalNotes = billingEvent.MedicalNotes;
        HasIep = billingEvent.HasIep;
        Documents = billingEvent.Documents ?? [];
        Notes = billingEvent.Notes ?? [];

        if (RegisteredAtUtc == default)
        {
            RegisteredAtUtc = billingEvent.OccurredUtc;
        }

        _events.Add(billingEvent);
    }
}
