namespace PhyrosClassroom.Billing.Engines.Default;

public sealed class ExternalBillingGatewayOptions
{
    public const string SectionName = "ExternalBillingGateway";

    public string ProviderName { get; set; } = "ManualOnly";
    public bool EnableLiveChargeCommands { get; set; }
    public string Mode { get; set; } = "Blocked";
    public string SimulatedExternalReferencePrefix { get; set; } = "sim";
}
