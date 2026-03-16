using PhyrosClassroom.Billing.Models;

namespace PhyrosClassroom.Billing.Orchestration;

public interface IListBillingUseCase
{
    Task<IReadOnlyList<BillingReadModel>> ExecuteAsync(CancellationToken cancellationToken = default);
}
