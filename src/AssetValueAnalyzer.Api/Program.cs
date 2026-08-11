using AssetValueAnalyzer.Infrastructure;
using AssetValueAnalyzer.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
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
app.MapHealthChecks("/health");
app.MapControllers();

app.Run();

public partial class Program;
