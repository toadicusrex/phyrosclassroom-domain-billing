using PhyrosClassroom.Billing.Models;

namespace PhyrosClassroom.Billing.Infrastructure.Persistence;

public interface IBillingLedgerStore
{
    Task<BillingLedger?> GetByRegistrationIdAsync(Guid registrationId, CancellationToken cancellationToken = default);

    Task<BillingLedger?> GetBySubjectIdAsync(string subjectId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BillingLedger>> ListAsync(CancellationToken cancellationToken = default);

    Task<BillingLedger> SaveAsync(BillingLedger ledger, CancellationToken cancellationToken = default);
}
