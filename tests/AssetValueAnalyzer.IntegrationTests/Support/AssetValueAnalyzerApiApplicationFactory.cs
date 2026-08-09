extern alias ApiApp;

using AssetValueAnalyzer.Application.ExchangeRates.Queries;
using AssetValueAnalyzer.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace AssetValueAnalyzer.IntegrationTests.Support;

public sealed class AssetValueAnalyzerApiApplicationFactory
    : WebApplicationFactory<ApiApp::Program>
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
                ["Finmaks:ApiKey"] = string.Empty
            });
        });
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IExchangeRateReader>();
            services.AddSingleton<IExchangeRateReader>(new FakeExchangeRateReader());
        });
    }

    private sealed class FakeExchangeRateReader : IExchangeRateReader
    {
        private static readonly ExchangeRateReadModel[] Rates =
        [
            CreateRate(1, 56, new DateTime(2026, 8, 8, 6, 0, 0), 45.75m),
            CreateRate(1, 56, new DateTime(2026, 8, 9, 6, 0, 0), 45.87m),
            CreateRate(4, 56, new DateTime(2026, 8, 9, 6, 0, 0), 53.42m)
        ];

        public Task<IReadOnlyList<ExchangeRateReadModel>> ReadLatestAsync(
            LatestExchangeRateQuery query,
            CancellationToken cancellationToken = default)
        {
            if (query.BaseCurrencyCode == 999)
            {
                throw new InvalidOperationException("Controlled API test failure.");
            }

            var filteredRates = Rates
                .Where(rate =>
                    (!query.BaseCurrencyCode.HasValue ||
                        rate.BaseCurrencyCode == query.BaseCurrencyCode) &&
                    (!query.ForeignCurrencyCode.HasValue ||
                        rate.ForeignCurrencyCode == query.ForeignCurrencyCode))
                .ToArray();
            var rateDate = query.RateDate ?? filteredRates
                .Select(rate => (DateOnly?)rate.RateDate)
                .Max();

            IReadOnlyList<ExchangeRateReadModel> result = rateDate is null
                ? []
                : filteredRates
                    .Where(rate => rate.RateDate == rateDate)
                    .OrderBy(rate => rate.BaseCurrencyCode)
                    .ThenBy(rate => rate.ForeignCurrencyCode)
                    .Take(query.Limit)
                    .ToArray();

            return Task.FromResult(result);
        }

        public Task<IReadOnlyList<ExchangeRateReadModel>> ReadRangeAsync(
            ExchangeRateRangeQuery query,
            CancellationToken cancellationToken = default)
        {
            var result = FilterCurrencyPairs(
                    query.BaseCurrencyCode,
                    query.ForeignCurrencyCode)
                .Where(rate =>
                    rate.RateDate >= query.StartDate &&
                    rate.RateDate <= query.EndDate)
                .OrderByDescending(rate => rate.RateDate)
                .ThenBy(rate => rate.BaseCurrencyCode)
                .ThenBy(rate => rate.ForeignCurrencyCode)
                .Take(query.Limit)
                .ToArray();

            return Task.FromResult<IReadOnlyList<ExchangeRateReadModel>>(result);
        }

        private static IEnumerable<ExchangeRateReadModel> FilterCurrencyPairs(
            int? baseCurrencyCode,
            int? foreignCurrencyCode) =>
            Rates.Where(rate =>
                (!baseCurrencyCode.HasValue ||
                    rate.BaseCurrencyCode == baseCurrencyCode) &&
                (!foreignCurrencyCode.HasValue ||
                    rate.ForeignCurrencyCode == foreignCurrencyCode));

        private static ExchangeRateReadModel CreateRate(
            int baseCurrencyCode,
            int foreignCurrencyCode,
            DateTime sourceUpdatedAt,
            decimal cashChangeRate) =>
            new(
                baseCurrencyCode,
                foreignCurrencyCode,
                DateOnly.FromDateTime(sourceUpdatedAt),
                ChangeRate: 44.90m,
                ExchangeRateValue: 46.10m,
                cashChangeRate,
                CashExchangeRate: 46.30m,
                CentralBankChangeRate: 45.00m,
                CentralBankExchangeRate: 45.20m,
                CrossRate: 1m,
                sourceUpdatedAt,
                new DateTimeOffset(sourceUpdatedAt.AddMinutes(1), TimeSpan.Zero));
    }
}
