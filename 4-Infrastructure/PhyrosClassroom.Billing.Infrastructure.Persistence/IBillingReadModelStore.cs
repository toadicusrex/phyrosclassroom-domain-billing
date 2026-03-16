using PhyrosClassroom.Billing.Models;

namespace PhyrosClassroom.Billing.Infrastructure.Persistence;

public interface IBillingReadModelStore
{
    Task UpsertAsync(BillingReadModel billing, CancellationToken cancellationToken = default);

    Task<BillingReadModel?> GetByIdAsync(Guid billingId, CancellationToken cancellationToken = default);

    Task<BillingReadModel?> GetBySourceReferenceAsync(string sourceSystem, string sourceReference, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BillingReadModel>> ListAsync(CancellationToken cancellationToken = default);
}
