using PhyrosClassroom.Billing.Infrastructure.Persistence;
using PhyrosClassroom.Billing.Models;

namespace PhyrosClassroom.Billing.Orchestration.Default;

public sealed class GetBillingReadModelBySubjectIdUseCase(IBillingReadModelStore readModelStore) : IGetBillingReadModelBySubjectIdUseCase
{
    private const string SourceSystem = "identity-subject";

    public Task<BillingReadModel?> ExecuteAsync(string subjectId, CancellationToken cancellationToken = default)
    {
        return readModelStore.GetBySourceReferenceAsync(SourceSystem, subjectId.Trim(), cancellationToken);
    }
}
