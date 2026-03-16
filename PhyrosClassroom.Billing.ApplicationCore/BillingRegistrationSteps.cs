using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PhyrosClassroom.Billing.Composition;
using PhyrosClassroom.Billing.Models;
using PhyrosClassroom.Billing.Orchestration;
using Reqnroll;

namespace PhyrosClassroom.Billing.ApplicationCore;

[Binding]
public sealed class BillingRegistrationSteps
{
    private ServiceProvider? _serviceProvider;
    private BillingAggregate? _registeredBilling;
    private BillingReadModel? _queriedBilling;

    [Given("the Billing application composition is configured")]
    public void GivenTheBillingApplicationCompositionIsConfigured()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BillingStorage:BasePath"] = Path.Combine(
                    Path.GetTempPath(),
                    "phyrosclassroom-billing-tests",
                    Guid.NewGuid().ToString("N")),
            })
            .Build();

        var services = new ServiceCollection();
        services.AddBillingCommandServices(configuration);
        services.AddBillingQueryServices(configuration);

        _serviceProvider = services.BuildServiceProvider();
    }

    [When("I register a billing named {string} {string}")]
    public async Task WhenIRegisterABillingNamed(string givenName, string familyName)
    {
        var useCase = _serviceProvider!.GetRequiredService<IRegisterBillingUseCase>();
        _registeredBilling = await useCase.ExecuteAsync(new RegisterBillingRequest(givenName, familyName));
    }

    [Then("the registered billing can be retrieved from the query side")]
    public async Task ThenTheRegisteredBillingCanBeRetrievedFromTheQuerySide()
    {
        var useCase = _serviceProvider!.GetRequiredService<IGetBillingReadModelByIdUseCase>();
        _queriedBilling = await useCase.ExecuteAsync(_registeredBilling!.BillingId);

        Assert.NotNull(_queriedBilling);
        Assert.Equal(_registeredBilling.BillingId, _queriedBilling!.BillingId);
        Assert.Equal(_registeredBilling.GivenName, _queriedBilling.GivenName);
        Assert.Equal(_registeredBilling.FamilyName, _queriedBilling.FamilyName);
    }
}
