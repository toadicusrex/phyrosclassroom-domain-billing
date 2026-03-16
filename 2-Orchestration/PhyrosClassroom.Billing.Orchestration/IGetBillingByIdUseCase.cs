using PhyrosClassroom.Billing.Models;

namespace PhyrosClassroom.Billing.Orchestration;

public interface IGetBillingByIdUseCase
{
    Task<BillingAggregate?> ExecuteAsync(Guid billingId, CancellationToken cancellationToken = default);
}
