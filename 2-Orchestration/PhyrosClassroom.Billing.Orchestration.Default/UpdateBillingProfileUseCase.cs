using PhyrosClassroom.Billing.Infrastructure.Persistence;
using PhyrosClassroom.Billing.Models;

namespace PhyrosClassroom.Billing.Orchestration.Default;

public sealed class UpdateBillingProfileUseCase(
    IBillingEventStore eventStore,
    IBillingReadModelStore readModelStore,
    IBillingHydratedModelCache hydratedModelCache) : IUpdateBillingProfileUseCase
{
    public async Task<BillingAggregate> ExecuteAsync(UpdateBillingProfileRequest request, CancellationToken cancellationToken = default)
    {
        var billing = await hydratedModelCache.GetAsync(request.BillingId, cancellationToken);
        if (billing is null)
        {
            var eventHistory = await eventStore.GetByIdAsync(request.BillingId, cancellationToken);
            billing = BillingAggregate.Rehydrate(eventHistory);
        }

        if (billing is null)
        {
            throw new InvalidOperationException("Billing was not found.");
        }

        billing.UpdateProfile(
            request.BillingName,
            request.WantsEmailNotifications,
            request.WantsSmsNotifications,
            request.OnboardingStatus,
            request.Contacts ?? [],
            request.Students ?? [],
            request.SourceRegistrationId,
            request.SourceRegistrationStatus,
            request.SourceRegistrationUpdatedAtUtc,
            request.BirthDate,
            request.GradeLevel,
            request.PrimaryGuardianName,
            request.PrimaryGuardianEmail,
            request.HasMedicalAlert,
            request.MedicalNotes,
            request.HasIep,
            request.Documents ?? [],
            request.Notes ?? [],
            DateTimeOffset.UtcNow);

        await eventStore.AppendAsync(billing.BillingId, [billing.Events[^1]], cancellationToken);
        await readModelStore.UpsertAsync(billing.ToReadModel(), cancellationToken);
        await hydratedModelCache.SetAsync(billing, cancellationToken);

        return billing;
    }
}
