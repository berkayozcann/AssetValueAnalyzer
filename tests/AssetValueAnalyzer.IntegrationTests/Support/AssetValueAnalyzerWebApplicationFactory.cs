extern alias WebApp;

using AssetValueAnalyzer.Application.ExchangeRates.Queries;
using AssetValueAnalyzer.Application.Reports.Creation;
using AssetValueAnalyzer.Infrastructure;
using AssetValueAnalyzer.Web.Hosting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace AssetValueAnalyzer.IntegrationTests.Support;

public sealed class AssetValueAnalyzerWebApplicationFactory
    : WebApplicationFactory<WebApp::Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{DependencyInjection.DatabaseConnectionName}"] =
                    "Server=integration-test;Database=AssetValueAnalyzer;Integrated Security=true;TrustServerCertificate=True",
                ["Finmaks:ApiKey"] = "integration-test-key"
            });
        });
        builder.ConfigureTestServices(services =>
        {
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
        });
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
