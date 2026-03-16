using PhyrosClassroom.Billing.Infrastructure.Persistence;
using PhyrosClassroom.Billing.Models;

namespace PhyrosClassroom.Billing.Orchestration.Default;

public sealed class GetBillingEventHistoryUseCase(IBillingEventStore eventStore) : IGetBillingEventHistoryUseCase
{
    public Task<IReadOnlyList<BillingEventRecord>> ExecuteAsync(Guid billingId, CancellationToken cancellationToken = default)
    {
        return eventStore.GetByIdAsync(billingId, cancellationToken);
    }
}
