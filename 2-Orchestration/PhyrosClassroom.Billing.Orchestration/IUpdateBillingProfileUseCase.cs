using PhyrosClassroom.Billing.Models;

namespace PhyrosClassroom.Billing.Orchestration;

public interface IUpdateBillingProfileUseCase
{
    Task<BillingAggregate> ExecuteAsync(UpdateBillingProfileRequest request, CancellationToken cancellationToken = default);
}
