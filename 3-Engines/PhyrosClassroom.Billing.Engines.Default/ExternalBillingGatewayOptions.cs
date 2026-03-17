namespace PhyrosClassroom.Billing.Engines.Default;

public sealed class ExternalBillingGatewayOptions
{
    public const string SectionName = "ExternalBillingGateway";

    public string ProviderName { get; set; } = "ManualOnly";
    public bool EnableLiveChargeCommands { get; set; }
    public string Mode { get; set; } = "Blocked";
    public string SimulatedExternalReferencePrefix { get; set; } = "sim";
    public string? BaseUrl { get; set; }
    public string ChargePath { get; set; } = "/charges";
    public string ApiKeyHeaderName { get; set; } = "X-API-Key";
    public string? ApiKey { get; set; }
    public string? BearerToken { get; set; }
    public string? MerchantKey { get; set; }
    public string? SourceKey { get; set; }
    public string? Pin { get; set; }
    public string ClerkName { get; set; } = "PhyrosClassroom";
    public string? ClientIpAddress { get; set; }
}
