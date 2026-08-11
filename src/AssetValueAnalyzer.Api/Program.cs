using AssetValueAnalyzer.Infrastructure;
using AssetValueAnalyzer.Infrastructure.Persistence;
using AssetValueAnalyzer.Api.HealthChecks;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseReadinessHealthCheck>(
        "database",
        tags: ["ready"]);
builder.Services.AddExchangeRateReadServices(builder.Configuration);

var app = builder.Build();
var httpsRedirectionEnabled = builder.Configuration.GetValue(
    "HttpsRedirection:Enabled",
    true);

await app.Services.EnsureDatabaseReadyAsync();

app.UseExceptionHandler();
if (httpsRedirectionEnabled)
{
    app.UseHttpsRedirection();
}
app.MapHealthChecks(
    "/health",
    new HealthCheckOptions
    {
        Predicate = registration => registration.Tags.Contains("ready")
    });
app.MapHealthChecks(
    "/health/live",
    new HealthCheckOptions
    {
        Predicate = _ => false
    });
app.MapControllers();

app.Run();

public partial class Program;
