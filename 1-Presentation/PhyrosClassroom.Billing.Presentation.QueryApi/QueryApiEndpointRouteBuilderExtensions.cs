using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PhyrosClassroom.Billing.Orchestration;

namespace PhyrosClassroom.Billing.Presentation.QueryApi;

public static class QueryApiEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapBillingQueryApi(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/query/billing");

        group.MapGet("/", async (
            IListBillingUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var billing = await useCase.ExecuteAsync(cancellationToken);
            return Results.Ok(billing);
        });

        group.MapGet("/{billingId:guid}", async (
            Guid billingId,
            IGetBillingReadModelByIdUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var billing = await useCase.ExecuteAsync(billingId, cancellationToken);
            return billing is null ? Results.NotFound() : Results.Ok(billing);
        });

        group.MapGet("/by-subject/{subjectId}", async (
            string subjectId,
            IGetBillingReadModelBySubjectIdUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var billing = await useCase.ExecuteAsync(subjectId, cancellationToken);
            return billing is null ? Results.NotFound() : Results.Ok(billing);
        });

        return endpoints;
    }
}
