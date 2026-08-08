using AssetValueAnalyzer.Domain.ExchangeRates;
using AssetValueAnalyzer.Infrastructure.Persistence;
using AssetValueAnalyzer.Infrastructure.Persistence.ExchangeRates;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AssetValueAnalyzer.IntegrationTests.ExchangeRates;

public sealed class EfCurrentUsdExchangeRateReaderTests
{
    [Fact]
    public async Task ReadAsync_ReturnsLatestUsdTryRateAndPreviousValue()
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
                CreateExchangeRate(1, 56, new DateTime(2026, 8, 7, 8, 0, 0), 46.75m),
                CreateExchangeRate(1, 56, new DateTime(2026, 8, 8, 8, 0, 0), 47.25m),
                CreateExchangeRate(4, 56, new DateTime(2026, 8, 8, 8, 0, 0), 55m));
            await setupContext.SaveChangesAsync(CancellationToken.None);
        }

        await using var queryContext = new AssetValueAnalyzerDbContext(options);
        var reader = new EfCurrentUsdExchangeRateReader(queryContext);

        var result = Assert.IsType<Application.ExchangeRates.Queries.CurrentUsdExchangeRate>(
            await reader.ReadAsync(CancellationToken.None));

        Assert.Equal(47.25m, result.Value);
        Assert.Equal(46.75m, result.PreviousValue);
        Assert.Equal(new DateOnly(2026, 8, 8), result.RateDate);
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
