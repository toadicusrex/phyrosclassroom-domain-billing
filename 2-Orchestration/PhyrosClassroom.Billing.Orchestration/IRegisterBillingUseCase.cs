using PhyrosClassroom.Billing.Models;

namespace PhyrosClassroom.Billing.Orchestration;

public interface IRegisterBillingUseCase
{
    Task<BillingAggregate> ExecuteAsync(RegisterBillingRequest request, CancellationToken cancellationToken = default);
}
