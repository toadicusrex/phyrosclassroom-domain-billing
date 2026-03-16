using PhyrosClassroom.Billing.Composition;
using PhyrosClassroom.Billing.Presentation.CommandApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddBillingCommandServices(builder.Configuration);

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new
{
    service = "phyrosclassroom-domain-billing-command",
    status = "ok",
    timestampUtc = DateTimeOffset.UtcNow,
}));
app.MapBillingCommandApi();

app.Run();
