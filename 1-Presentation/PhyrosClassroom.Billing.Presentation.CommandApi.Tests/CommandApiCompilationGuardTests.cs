namespace PhyrosClassroom.Billing.Presentation.CommandApi.Tests;

public sealed class CommandApiCompilationGuardTests
{
    [Fact]
    public void PresentationAssembly_IsLoadable()
    {
        var assembly = typeof(PhyrosClassroom.Billing.Presentation.CommandApi.CommandApiEndpointRouteBuilderExtensions).Assembly;

        Assert.NotNull(assembly);
    }
}
