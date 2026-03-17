using PhyrosClassroom.Billing.Models;

namespace PhyrosClassroom.Billing.Orchestration;

public interface IInitializeBillingLedgerUseCase
{
    Task<BillingLedger> ExecuteAsync(InitializeBillingLedgerRequest request, CancellationToken cancellationToken = default);
}

public interface IGetBillingLedgerByRegistrationIdUseCase
{
    Task<BillingLedger?> ExecuteAsync(Guid registrationId, CancellationToken cancellationToken = default);
}

public interface IGetBillingLedgerBySubjectIdUseCase
{
    Task<BillingLedger?> ExecuteAsync(string subjectId, CancellationToken cancellationToken = default);
}

public interface ICreateBillingInvoiceUseCase
{
    Task<BillingLedger> ExecuteAsync(CreateBillingInvoiceRequest request, CancellationToken cancellationToken = default);
}

public interface IRecordBillingPaymentUseCase
{
    Task<BillingLedger> ExecuteAsync(RecordBillingPaymentRequest request, CancellationToken cancellationToken = default);
}

public interface IChargeBillingInvoiceUseCase
{
    Task<BillingLedger> ExecuteAsync(ChargeBillingInvoiceRequest request, CancellationToken cancellationToken = default);
}

public interface IUpdateBillingPaymentPlanUseCase
{
    Task<BillingLedger> ExecuteAsync(UpdateBillingPaymentPlanRequest request, CancellationToken cancellationToken = default);
}

public interface IUpsertBillingPaymentMethodUseCase
{
    Task<BillingLedger> ExecuteAsync(UpsertBillingPaymentMethodRequest request, CancellationToken cancellationToken = default);
}
