using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PhyrosClassroom.Billing.Engines;
using PhyrosClassroom.Billing.Engines.Default;
using PhyrosClassroom.Billing.Infrastructure.Persistence;
using PhyrosClassroom.Billing.Infrastructure.Persistence.Default;
using PhyrosClassroom.Billing.Orchestration;
using PhyrosClassroom.Billing.Orchestration.Default;

namespace PhyrosClassroom.Billing.Composition;

public static class BillingServiceCollectionExtensions
{
    public static IServiceCollection AddBillingCommandServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddBillingSharedServices(configuration);
        services.AddScoped<IRegisterBillingUseCase, RegisterBillingUseCase>();
        services.AddScoped<IUpdateBillingProfileUseCase, UpdateBillingProfileUseCase>();
        services.AddScoped<IGetBillingByIdUseCase, GetBillingByIdUseCase>();
        services.AddScoped<IGetBillingAtPointInTimeUseCase, GetBillingAtPointInTimeUseCase>();
        services.AddScoped<IGetBillingEventHistoryUseCase, GetBillingEventHistoryUseCase>();

        return services;
    }

    public static IServiceCollection AddBillingQueryServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddBillingSharedServices(configuration);
        services.AddScoped<IListBillingUseCase, ListBillingUseCase>();
        services.AddScoped<IGetBillingReadModelByIdUseCase, GetBillingReadModelByIdUseCase>();
        services.AddScoped<IGetBillingReadModelBySubjectIdUseCase, GetBillingReadModelBySubjectIdUseCase>();

        return services;
    }

    private static IServiceCollection AddBillingSharedServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<BillingStorageOptions>()
            .Bind(configuration.GetSection(BillingStorageOptions.SectionName));

        services.AddSingleton<IBillingEventStore, FileBillingEventStore>();
        services.AddSingleton<IBillingReadModelStore, FileBillingReadModelStore>();
        services.AddSingleton<IBillingHydratedModelCache, InMemoryBillingHydratedModelCache>();
        services.AddSingleton<IBillingCodeGenerator, BillingCodeGenerator>();

        return services;
    }
}
