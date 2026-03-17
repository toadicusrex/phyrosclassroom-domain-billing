using Microsoft.Extensions.Options;
using PhyrosClassroom.Billing.Engines;
using PhyrosClassroom.Billing.Models;

namespace PhyrosClassroom.Billing.Engines.Default;

public sealed class SafetyFirstExternalBillingGateway(IOptions<ExternalBillingGatewayOptions> options) : IExternalBillingGateway
{
    public Task<ExternalBillingChargeResult> ChargeInvoiceAsync(
        BillingLedger ledger,
        BillingInvoice invoice,
        ExternalBillingChargeRequest request,
        CancellationToken cancellationToken = default)
    {
        var configuredOptions = options.Value;

        if (!configuredOptions.EnableLiveChargeCommands)
        {
            return Task.FromResult(new ExternalBillingChargeResult(
                false,
                configuredOptions.ProviderName,
                "Blocked",
                null,
                "Live processor charging is disabled. Enable ExternalBillingGateway:EnableLiveChargeCommands explicitly before using invoice charge commands."));
        }

        var mode = Normalize(configuredOptions.Mode);
        if (mode is "simulatedsuccess" or "simulatedapproval")
        {
            var externalReferencePrefix = string.IsNullOrWhiteSpace(configuredOptions.SimulatedExternalReferencePrefix)
                ? "sim"
                : configuredOptions.SimulatedExternalReferencePrefix.Trim();

            return Task.FromResult(new ExternalBillingChargeResult(
                true,
                configuredOptions.ProviderName,
                "Approved",
                $"{externalReferencePrefix}-{request.IdempotencyKey}",
                null));
        }

        if (mode is "simulateddecline" or "simulatedfailure")
        {
            return Task.FromResult(new ExternalBillingChargeResult(
                false,
                configuredOptions.ProviderName,
                "Declined",
                null,
                "The configured billing gateway is running in simulated decline mode."));
        }

        if (mode is "legacyusepay" or "usepay")
        {
            return Task.FromResult(new ExternalBillingChargeResult(
                false,
                "USePay",
                "NotConfigured",
                null,
                "Legacy USePay charging has not been wired into this service yet. Keep live charging disabled or use simulated modes until the provider integration is implemented."));
        }

        return Task.FromResult(new ExternalBillingChargeResult(
            false,
            configuredOptions.ProviderName,
            "NotImplemented",
            null,
            "No live billing gateway implementation has been configured for this provider."));
    }

    private static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? "blocked"
            : new string(value.Where(character => !char.IsWhiteSpace(character) && character is not '-' and not '_').ToArray()).ToLowerInvariant();
}
