using AssetValueAnalyzer.Domain.ExchangeRates;
using AssetValueAnalyzer.Infrastructure.Persistence;
using AssetValueAnalyzer.Infrastructure.Persistence.ExchangeRates;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AssetValueAnalyzer.IntegrationTests.ExchangeRates;

public sealed class EfUsdCashChangeRateReaderTests
{
    [Fact]
    public async Task ReadAsync_ReturnsOnlyUsdTryCashChangeRatesInsideRequestedRange()
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
                CreateExchangeRate(1, 56, new DateTime(2022, 1, 28), 13.10m),
                CreateExchangeRate(1, 56, new DateTime(2022, 1, 31), 13.25m),
                CreateExchangeRate(1, 56, new DateTime(2022, 2, 1), 13.40m),
                CreateExchangeRate(4, 56, new DateTime(2022, 1, 31), 15.00m));
            await setupContext.SaveChangesAsync(CancellationToken.None);
        }

        await using var queryContext = new AssetValueAnalyzerDbContext(options);
        var reader = new EfUsdCashChangeRateReader(queryContext);

        var result = await reader.ReadAsync(
            new DateOnly(2022, 1, 29),
            new DateOnly(2022, 1, 31),
            CancellationToken.None);

        var rate = Assert.Single(result);
        Assert.Equal(new DateOnly(2022, 1, 31), rate.RateDate);
        Assert.Equal(13.25m, rate.Value);
    }

    private static ExchangeRate CreateExchangeRate(
        int baseCurrencyCode,
        int foreignCurrencyCode,
        DateTime sourceUpdatedAt,
        decimal cashChangeRate) =>
        new(
            baseCurrencyCode,
            foreignCurrencyCode,
            sourceUpdatedAt,
            new DateTimeOffset(sourceUpdatedAt, TimeSpan.Zero),
            changeRate: 12m,
            exchangeRateValue: 14m,
            cashChangeRate,
            cashExchangeRate: 15m,
            centralBankChangeRate: 12.5m,
            centralBankExchangeRate: 13.5m,
            crossRate: 1m);
}
