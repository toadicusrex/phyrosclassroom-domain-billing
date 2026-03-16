namespace PhyrosClassroom.Billing.Presentation.QueryApi.Tests;

public sealed class QueryApiCompilationGuardTests
{
    [Fact]
    public void PresentationAssembly_IsLoadable()
    {
        var assembly = typeof(PhyrosClassroom.Billing.Presentation.QueryApi.QueryApiEndpointRouteBuilderExtensions).Assembly;

        Assert.NotNull(assembly);
    }
}
