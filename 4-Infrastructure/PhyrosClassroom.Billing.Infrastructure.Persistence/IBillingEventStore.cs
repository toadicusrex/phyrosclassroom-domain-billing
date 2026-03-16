using PhyrosClassroom.Billing.Models;

namespace PhyrosClassroom.Billing.Infrastructure.Persistence;

public interface IBillingEventStore
{
    Task AppendAsync(Guid billingId, IReadOnlyList<BillingEventRecord> events, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BillingEventRecord>> GetByIdAsync(Guid billingId, CancellationToken cancellationToken = default);
}
