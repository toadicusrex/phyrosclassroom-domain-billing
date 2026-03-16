using PhyrosClassroom.Billing.Models;

namespace PhyrosClassroom.Billing.Orchestration;

public interface IGetBillingReadModelByIdUseCase
{
    Task<BillingReadModel?> ExecuteAsync(Guid billingId, CancellationToken cancellationToken = default);
}
