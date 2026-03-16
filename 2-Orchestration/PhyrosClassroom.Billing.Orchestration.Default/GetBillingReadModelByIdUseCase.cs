using PhyrosClassroom.Billing.Infrastructure.Persistence;
using PhyrosClassroom.Billing.Models;

namespace PhyrosClassroom.Billing.Orchestration.Default;

public sealed class GetBillingReadModelByIdUseCase(IBillingReadModelStore readModelStore) : IGetBillingReadModelByIdUseCase
{
    public Task<BillingReadModel?> ExecuteAsync(Guid billingId, CancellationToken cancellationToken = default)
    {
        return readModelStore.GetByIdAsync(billingId, cancellationToken);
    }
}
