using PhyrosClassroom.Billing.Infrastructure.Persistence;
using PhyrosClassroom.Billing.Models;

namespace PhyrosClassroom.Billing.Orchestration.Default;

public sealed class GetBillingAtPointInTimeUseCase(IBillingEventStore eventStore) : IGetBillingAtPointInTimeUseCase
{
    public async Task<BillingAggregate?> ExecuteAsync(
        Guid billingId,
        DateTimeOffset pointInTimeUtc,
        CancellationToken cancellationToken = default)
    {
        var eventHistory = await eventStore.GetByIdAsync(billingId, cancellationToken);
        return BillingAggregate.RehydrateAt(eventHistory, pointInTimeUtc);
    }
}
