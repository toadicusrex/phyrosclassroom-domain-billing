using PhyrosClassroom.Billing.Models;

namespace PhyrosClassroom.Billing.Infrastructure.Persistence;

public interface IBillingHydratedModelCache
{
    Task<BillingAggregate?> GetAsync(Guid billingId, CancellationToken cancellationToken = default);

    Task SetAsync(BillingAggregate billing, CancellationToken cancellationToken = default);
}
