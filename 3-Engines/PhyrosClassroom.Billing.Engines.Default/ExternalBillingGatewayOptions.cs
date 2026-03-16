namespace PhyrosClassroom.Billing.Engines.Default;

public sealed class ExternalBillingGatewayOptions
{
    public const string SectionName = "ExternalBillingGateway";

    public string ProviderName { get; set; } = "ManualOnly";
    public bool EnableLiveChargeCommands { get; set; }
}
