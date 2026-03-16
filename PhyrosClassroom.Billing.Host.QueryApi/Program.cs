using PhyrosClassroom.Billing.Composition;
using PhyrosClassroom.Billing.Presentation.QueryApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddBillingQueryServices(builder.Configuration);

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new
{
    service = "phyrosclassroom-domain-billing-query",
    status = "ok",
    timestampUtc = DateTimeOffset.UtcNow,
}));
app.MapBillingQueryApi();

app.Run();
