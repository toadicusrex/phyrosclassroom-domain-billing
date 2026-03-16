namespace PhyrosClassroom.Billing.Infrastructure.Persistence.Default;

public sealed class BillingStorageOptions
{
    public const string SectionName = "BillingStorage";

    public string BasePath { get; set; } = "App_Data";
}
