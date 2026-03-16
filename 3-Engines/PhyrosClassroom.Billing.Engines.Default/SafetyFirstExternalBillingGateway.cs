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

        return Task.FromResult(new ExternalBillingChargeResult(
            false,
            configuredOptions.ProviderName,
            "NotImplemented",
            null,
            "No live billing gateway implementation has been configured for this provider."));
    }
}
