using PhyrosClassroom.Billing.Infrastructure.Persistence;
using PhyrosClassroom.Billing.Models;

namespace PhyrosClassroom.Billing.Orchestration.Default;

public sealed class ListBillingUseCase(IBillingReadModelStore readModelStore) : IListBillingUseCase
{
    public Task<IReadOnlyList<BillingReadModel>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        return readModelStore.ListAsync(cancellationToken);
    }
}
