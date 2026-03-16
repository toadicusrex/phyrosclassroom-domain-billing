using PhyrosClassroom.Billing.Infrastructure.Persistence;
using PhyrosClassroom.Billing.Models;

namespace PhyrosClassroom.Billing.Infrastructure.Persistence.Default;

public sealed class InMemoryBillingHydratedModelCache : IBillingHydratedModelCache
{
    private readonly Dictionary<Guid, BillingAggregate> _billing = [];
    private readonly object _gate = new();

    public Task<BillingAggregate?> GetAsync(Guid billingId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _billing.TryGetValue(billingId, out var billing);
            return Task.FromResult(billing);
        }
    }

    public Task SetAsync(BillingAggregate billing, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _billing[billing.BillingId] = billing;
        }

        return Task.CompletedTask;
    }
}
