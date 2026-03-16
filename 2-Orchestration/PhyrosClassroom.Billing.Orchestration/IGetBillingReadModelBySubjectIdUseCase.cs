using PhyrosClassroom.Billing.Models;

namespace PhyrosClassroom.Billing.Orchestration;

public interface IGetBillingReadModelBySubjectIdUseCase
{
    Task<BillingReadModel?> ExecuteAsync(string subjectId, CancellationToken cancellationToken = default);
}
