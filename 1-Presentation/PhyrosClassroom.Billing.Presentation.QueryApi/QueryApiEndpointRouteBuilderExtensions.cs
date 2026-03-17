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

        group.MapGet("/registrations/{registrationId:guid}", async (
            Guid registrationId,
            IGetBillingLedgerByRegistrationIdUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var billing = await useCase.ExecuteAsync(registrationId, cancellationToken);
            return billing is null ? Results.NotFound() : Results.Ok(billing);
        });

        group.MapGet("/ledgers/by-subject/{subjectId}", async (
            string subjectId,
            IGetBillingLedgerBySubjectIdUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var billing = await useCase.ExecuteAsync(subjectId, cancellationToken);
            return billing is null ? Results.NotFound() : Results.Ok(billing);
        });

        group.MapGet("/operations/summary", async (
            DateOnly? asOfDate,
            IGetBillingOperationsSummaryUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var summary = await useCase.ExecuteAsync(asOfDate, cancellationToken);
            return Results.Ok(summary);
        });

        group.MapGet("/operations/overdue", async (
            DateOnly? asOfDate,
            IListBillingOverdueInvoicesUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var overdueInvoices = await useCase.ExecuteAsync(asOfDate, cancellationToken);
            return Results.Ok(overdueInvoices);
        });

        group.MapGet("/operations/charge-reviews", async (
            bool includeResolved,
            IListBillingChargeReviewQueueUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var chargeReviews = await useCase.ExecuteAsync(includeResolved, cancellationToken);
            return Results.Ok(chargeReviews);
        });

        group.MapGet("/operations/reconciliation-summary", async (
            IGetBillingReconciliationSummaryUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var summary = await useCase.ExecuteAsync(cancellationToken);
            return Results.Ok(summary);
        });

        return endpoints;
    }
}
