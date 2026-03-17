using PhyrosClassroom.Billing.Engines;
using PhyrosClassroom.Billing.Models;

namespace PhyrosClassroom.Billing.Engines.Default;

public sealed class SafetyFirstExternalBillingGateway(
    HttpExternalBillingGateway httpGateway,
    Microsoft.Extensions.Options.IOptions<ExternalBillingGatewayOptions> options) : IExternalBillingGateway
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
            return httpGateway.ChargeInvoiceAsync(ledger, invoice, request, cancellationToken);
        }

        if (mode is "http" or "live" or "external")
        {
            return httpGateway.ChargeInvoiceAsync(ledger, invoice, request, cancellationToken);
        }

        return Task.FromResult(new ExternalBillingChargeResult(
            false,
            configuredOptions.ProviderName,
            "NotConfigured",
            null,
            "No live billing gateway mode has been configured. Use ExternalBillingGateway:Mode=http or usepay and provide the required connection settings."));
    }

    private static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? "blocked"
            : new string(value.Where(character => !char.IsWhiteSpace(character) && character is not '-' and not '_').ToArray()).ToLowerInvariant();
}
