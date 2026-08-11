using AssetValueAnalyzer.Application.ExchangeRates.Synchronization;
using AssetValueAnalyzer.Infrastructure;
using AssetValueAnalyzer.Infrastructure.Persistence;
using AssetValueAnalyzer.Web.Features.ExchangeRates.Realtime;
using AssetValueAnalyzer.Web.Features.Reports;
using AssetValueAnalyzer.Web.Hosting;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();
builder.Services.AddHealthChecks();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".AssetValueAnalyzer.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.IdleTimeout = TimeSpan.FromHours(2);
});
builder.Services.AddScoped<IReportWorkspaceSession, ReportWorkspaceSession>();
builder.Services.AddSingleton<
    IExchangeRateSynchronizationNotifier,
    SignalRExchangeRateSynchronizationNotifier>();
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddHostedService<ExchangeRateInitializationHostedService>();

var app = builder.Build();
var httpsRedirectionEnabled = builder.Configuration.GetValue(
    "HttpsRedirection:Enabled",
    true);

await app.Services.ApplyDatabaseMigrationsAsync();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

if (httpsRedirectionEnabled)
{
    app.UseHttpsRedirection();
}
app.UseRouting();

app.UseSession();
app.UseAuthorization();

app.MapStaticAssets();

app.MapHealthChecks("/health");

app.MapHub<ExchangeRateHub>("/hubs/exchange-rates");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();

public partial class Program;
