using PhyrosClassroom.Billing.Models;

namespace PhyrosClassroom.Billing.Orchestration;

public interface IGetBillingAtPointInTimeUseCase
{
    Task<BillingAggregate?> ExecuteAsync(Guid billingId, DateTimeOffset pointInTimeUtc, CancellationToken cancellationToken = default);
}
