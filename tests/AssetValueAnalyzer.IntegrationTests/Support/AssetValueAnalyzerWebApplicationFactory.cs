extern alias WebApp;

using AssetValueAnalyzer.Application.ExchangeRates.External;
using AssetValueAnalyzer.Application.ExchangeRates.Queries;
using AssetValueAnalyzer.Application.Reports.Creation;
using AssetValueAnalyzer.Web.Hosting;
using AssetValueAnalyzer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace AssetValueAnalyzer.IntegrationTests.Support;

public sealed class AssetValueAnalyzerWebApplicationFactory
    : WebApplicationFactory<WebApp::Program>
{
    public const string TestingEnvironmentName = "Testing";

    public AssetValueAnalyzerWebApplicationFactory()
        : this(new DatabaseStartupProbe())
    {
    }

    internal AssetValueAnalyzerWebApplicationFactory(DatabaseStartupProbe databaseStartup)
    {
        DatabaseStartup = databaseStartup;
    }

    public DatabaseStartupProbe DatabaseStartup { get; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(TestingEnvironmentName);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IDatabaseStartupService>();
            services.AddSingleton<IDatabaseStartupService>(DatabaseStartup);

            var initializationHostedService = services.SingleOrDefault(descriptor =>
                descriptor.ServiceType == typeof(IHostedService) &&
                descriptor.ImplementationType == typeof(ExchangeRateInitializationHostedService));

            if (initializationHostedService is not null)
            {
                services.Remove(initializationHostedService);
            }

            services.RemoveAll<ICurrentUsdExchangeRateReader>();
            services.AddSingleton<ICurrentUsdExchangeRateReader>(
                new FakeCurrentUsdExchangeRateReader());
            services.RemoveAll<IUsdCashChangeRateReader>();
            services.AddSingleton<IUsdCashChangeRateReader>(
                new FakeUsdCashChangeRateReader(
                [
                    new(new DateOnly(2021, 12, 31), 10m),
                    new(new DateOnly(2022, 1, 31), 20m)
                ]));
            services.RemoveAll<IFinmaksExchangeRateClient>();
            services.AddSingleton<IFinmaksExchangeRateClient>(
                new BlockedFinmaksExchangeRateClient());
        });
    }

    private sealed class BlockedFinmaksExchangeRateClient : IFinmaksExchangeRateClient
    {
        public Task<IReadOnlyList<ExchangeRateQuote>> GetRatesAsync(
            DateOnly? startDate = null,
            DateOnly? endDate = null,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(
                "The integration-test host must not call the real Finmaks service.");
    }

    private sealed class FakeUsdCashChangeRateReader(
        IReadOnlyList<UsdCashChangeRate> rates) : IUsdCashChangeRateReader
    {
        public Task<IReadOnlyList<UsdCashChangeRate>> ReadAsync(
            DateOnly startDate,
            DateOnly endDate,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<UsdCashChangeRate> result = rates
                .Where(rate => rate.RateDate >= startDate && rate.RateDate <= endDate)
                .ToArray();

            return Task.FromResult(result);
        }
    }
}
