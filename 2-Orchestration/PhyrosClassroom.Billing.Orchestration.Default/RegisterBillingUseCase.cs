using PhyrosClassroom.Billing.Engines;
using PhyrosClassroom.Billing.Infrastructure.Persistence;
using PhyrosClassroom.Billing.Models;

namespace PhyrosClassroom.Billing.Orchestration.Default;

public sealed class RegisterBillingUseCase(
    IBillingCodeGenerator codeGenerator,
    IBillingEventStore eventStore,
    IBillingReadModelStore readModelStore,
    IBillingHydratedModelCache hydratedModelCache) : IRegisterBillingUseCase
{
    private const string IdentitySubjectSourceSystem = "identity-subject";

    public async Task<BillingAggregate> ExecuteAsync(RegisterBillingRequest request, CancellationToken cancellationToken = default)
    {
        var sourceSystem = string.IsNullOrWhiteSpace(request.SourceSystem) ? IdentitySubjectSourceSystem : request.SourceSystem.Trim();
        var sourceReference = string.IsNullOrWhiteSpace(request.SourceReference) ? request.SubjectId.Trim() : request.SourceReference.Trim();

        if (sourceSystem is not null && sourceReference is not null)
        {
            var existingReadModel = await readModelStore.GetBySourceReferenceAsync(sourceSystem, sourceReference, cancellationToken);
            if (existingReadModel is not null)
            {
                var cachedBilling = await hydratedModelCache.GetAsync(existingReadModel.BillingId, cancellationToken);
                if (cachedBilling is not null)
                {
                    return cachedBilling;
                }

                var eventHistory = await eventStore.GetByIdAsync(existingReadModel.BillingId, cancellationToken);
                var hydratedBilling = BillingAggregate.Rehydrate(eventHistory);
                if (hydratedBilling is not null)
                {
                    await hydratedModelCache.SetAsync(hydratedBilling, cancellationToken);
                    return hydratedBilling;
                }
            }
        }

        var billingId = Guid.NewGuid();
        var occurredUtc = DateTimeOffset.UtcNow;
        var billingCode = codeGenerator.GenerateCode(request.GivenName, request.FamilyName, occurredUtc);

        var billing = BillingAggregate.Register(
            billingId,
            billingCode,
            request.GivenName,
            request.FamilyName,
            occurredUtc,
            request.SubjectId,
            request.BillingName,
            request.WantsEmailNotifications,
            request.WantsSmsNotifications,
            request.OnboardingStatus,
            request.Contacts,
            request.Students,
            request.SourceRegistrationId,
            request.SourceRegistrationStatus,
            request.SourceRegistrationUpdatedAtUtc,
            sourceSystem,
            sourceReference);

        await eventStore.AppendAsync(billing.BillingId, billing.Events, cancellationToken);
        await readModelStore.UpsertAsync(billing.ToReadModel(), cancellationToken);
        await hydratedModelCache.SetAsync(billing, cancellationToken);

        return billing;
    }
}
