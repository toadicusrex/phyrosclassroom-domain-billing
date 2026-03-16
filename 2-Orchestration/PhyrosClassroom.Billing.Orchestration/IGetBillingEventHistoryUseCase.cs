using PhyrosClassroom.Billing.Models;

namespace PhyrosClassroom.Billing.Orchestration;

public interface IGetBillingEventHistoryUseCase
{
    Task<IReadOnlyList<BillingEventRecord>> ExecuteAsync(Guid billingId, CancellationToken cancellationToken = default);
}
