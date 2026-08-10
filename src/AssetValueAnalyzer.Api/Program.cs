using AssetValueAnalyzer.Infrastructure;
using AssetValueAnalyzer.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExchangeRateReadServices(builder.Configuration);

var app = builder.Build();

await app.Services.EnsureDatabaseReadyAsync();

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.MapControllers();

app.Run();

public partial class Program;
