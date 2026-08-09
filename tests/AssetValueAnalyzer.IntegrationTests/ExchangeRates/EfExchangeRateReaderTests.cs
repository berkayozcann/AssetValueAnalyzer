using AssetValueAnalyzer.Application.ExchangeRates.Queries;
using AssetValueAnalyzer.Domain.ExchangeRates;
using AssetValueAnalyzer.Infrastructure.Persistence;
using AssetValueAnalyzer.Infrastructure.Persistence.ExchangeRates;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AssetValueAnalyzer.IntegrationTests.ExchangeRates;

public sealed class EfExchangeRateReaderTests
{
    [Fact]
    public async Task ReadLatestAsync_UsesLatestDateMatchingCurrencyFilters()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(CancellationToken.None);
        var options = new DbContextOptionsBuilder<AssetValueAnalyzerDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var setupContext = new AssetValueAnalyzerDbContext(options))
        {
            await setupContext.Database.EnsureCreatedAsync(CancellationToken.None);
            setupContext.ExchangeRates.AddRange(
                CreateRate(1, 56, new DateTime(2026, 8, 8, 6, 0, 0), 45.75m),
                CreateRate(1, 56, new DateTime(2026, 8, 9, 6, 0, 0), 45.87m),
                CreateRate(4, 56, new DateTime(2026, 8, 10, 6, 0, 0), 53.42m));
            await setupContext.SaveChangesAsync(CancellationToken.None);
        }

        await using var queryContext = new AssetValueAnalyzerDbContext(options);
        var reader = new EfExchangeRateReader(queryContext);

        var result = await reader.ReadLatestAsync(
            new LatestExchangeRateQuery(
                RateDate: null,
                BaseCurrencyCode: 1,
                ForeignCurrencyCode: 56,
                Limit: 100),
            CancellationToken.None);

        var rate = Assert.Single(result);
        Assert.Equal(new DateOnly(2026, 8, 9), rate.RateDate);
        Assert.Equal(45.87m, rate.CashChangeRate);
    }

    [Fact]
    public async Task ReadLatestAsync_AppliesExplicitDateOrderingAndLimit()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(CancellationToken.None);
        var options = new DbContextOptionsBuilder<AssetValueAnalyzerDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var setupContext = new AssetValueAnalyzerDbContext(options))
        {
            await setupContext.Database.EnsureCreatedAsync(CancellationToken.None);
            setupContext.ExchangeRates.AddRange(
                CreateRate(4, 56, new DateTime(2026, 8, 9, 6, 0, 0), 53.42m),
                CreateRate(1, 56, new DateTime(2026, 8, 9, 6, 0, 0), 45.87m));
            await setupContext.SaveChangesAsync(CancellationToken.None);
        }

        await using var queryContext = new AssetValueAnalyzerDbContext(options);
        var reader = new EfExchangeRateReader(queryContext);

        var result = await reader.ReadLatestAsync(
            new LatestExchangeRateQuery(
                RateDate: new DateOnly(2026, 8, 9),
                BaseCurrencyCode: null,
                ForeignCurrencyCode: 56,
                Limit: 1),
            CancellationToken.None);

        var rate = Assert.Single(result);
        Assert.Equal(1, rate.BaseCurrencyCode);
        Assert.Equal(56, rate.ForeignCurrencyCode);
    }

    [Fact]
    public async Task ReadRangeAsync_AppliesInclusiveRangeCurrencyFiltersAndOrdering()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(CancellationToken.None);
        var options = new DbContextOptionsBuilder<AssetValueAnalyzerDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var setupContext = new AssetValueAnalyzerDbContext(options))
        {
            await setupContext.Database.EnsureCreatedAsync(CancellationToken.None);
            setupContext.ExchangeRates.AddRange(
                CreateRate(1, 56, new DateTime(2026, 8, 7, 6, 0, 0), 45.50m),
                CreateRate(1, 56, new DateTime(2026, 8, 8, 6, 0, 0), 45.75m),
                CreateRate(1, 56, new DateTime(2026, 8, 9, 6, 0, 0), 45.87m),
                CreateRate(4, 56, new DateTime(2026, 8, 9, 6, 0, 0), 53.42m));
            await setupContext.SaveChangesAsync(CancellationToken.None);
        }

        await using var queryContext = new AssetValueAnalyzerDbContext(options);
        var reader = new EfExchangeRateReader(queryContext);

        var result = await reader.ReadRangeAsync(
            new ExchangeRateRangeQuery(
                StartDate: new DateOnly(2026, 8, 8),
                EndDate: new DateOnly(2026, 8, 9),
                BaseCurrencyCode: 1,
                ForeignCurrencyCode: 56,
                Limit: 2),
            CancellationToken.None);

        Assert.Collection(
            result,
            rate => Assert.Equal(new DateOnly(2026, 8, 9), rate.RateDate),
            rate => Assert.Equal(new DateOnly(2026, 8, 8), rate.RateDate));
        Assert.All(result, rate =>
        {
            Assert.Equal(1, rate.BaseCurrencyCode);
            Assert.Equal(56, rate.ForeignCurrencyCode);
        });
    }

    private static ExchangeRate CreateRate(
        int baseCurrencyCode,
        int foreignCurrencyCode,
        DateTime sourceUpdatedAt,
        decimal cashChangeRate) =>
        new(
            baseCurrencyCode,
            foreignCurrencyCode,
            sourceUpdatedAt,
            new DateTimeOffset(sourceUpdatedAt.AddMinutes(1), TimeSpan.Zero),
            changeRate: 44.90m,
            exchangeRateValue: 46.10m,
            cashChangeRate,
            cashExchangeRate: 46.30m,
            centralBankChangeRate: 45.00m,
            centralBankExchangeRate: 45.20m,
            crossRate: 1m);
}
