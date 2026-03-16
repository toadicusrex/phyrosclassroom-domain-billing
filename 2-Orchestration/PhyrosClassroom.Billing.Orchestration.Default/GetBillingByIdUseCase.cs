using PhyrosClassroom.Billing.Infrastructure.Persistence;
using PhyrosClassroom.Billing.Models;

namespace PhyrosClassroom.Billing.Orchestration.Default;

public sealed class GetBillingByIdUseCase(
    IBillingEventStore eventStore,
    IBillingHydratedModelCache hydratedModelCache) : IGetBillingByIdUseCase
{
    public async Task<BillingAggregate?> ExecuteAsync(Guid billingId, CancellationToken cancellationToken = default)
    {
        var cachedBilling = await hydratedModelCache.GetAsync(billingId, cancellationToken);
        if (cachedBilling is not null)
        {
            return cachedBilling;
        }

        var eventHistory = await eventStore.GetByIdAsync(billingId, cancellationToken);
        var billing = BillingAggregate.Rehydrate(eventHistory);

        if (billing is not null)
        {
            await hydratedModelCache.SetAsync(billing, cancellationToken);
        }

        return billing;
    }
}
