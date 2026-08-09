using AssetValueAnalyzer.Domain.ExchangeRates;
using AssetValueAnalyzer.Infrastructure.Persistence;
using AssetValueAnalyzer.Infrastructure.Persistence.ExchangeRates;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AssetValueAnalyzer.IntegrationTests.ExchangeRates;

public sealed class EfExchangeRateStoreTests
{
    [Fact]
    public async Task GetLatestRateDateAsync_ReturnsTheMostRecentStoredDate()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(CancellationToken.None);
        var dbContextOptions = new DbContextOptionsBuilder<AssetValueAnalyzerDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var setupContext = new AssetValueAnalyzerDbContext(dbContextOptions))
        {
            await setupContext.Database.EnsureCreatedAsync(CancellationToken.None);
            setupContext.ExchangeRates.AddRange(
                CreateExchangeRate(
                    new DateTime(2026, 8, 5, 6, 0, 0),
                    new DateTimeOffset(2026, 8, 5, 6, 1, 0, TimeSpan.Zero),
                    46.10000m),
                CreateExchangeRate(
                    new DateTime(2026, 8, 7, 6, 0, 0),
                    new DateTimeOffset(2026, 8, 7, 6, 1, 0, TimeSpan.Zero),
                    46.50000m));
            await setupContext.SaveChangesAsync(CancellationToken.None);
        }

        await using var queryContext = new AssetValueAnalyzerDbContext(dbContextOptions);
        var store = new EfExchangeRateStore(queryContext);

        var latestRateDate = await store.GetLatestRateDateAsync(CancellationToken.None);

        Assert.Equal(new DateOnly(2026, 8, 7), latestRateDate);
    }

    [Fact]
    public async Task UpsertAsync_InsertsThenUpdatesTheSameBusinessKey()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(CancellationToken.None);
        var dbContextOptions = new DbContextOptionsBuilder<AssetValueAnalyzerDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var setupContext = new AssetValueAnalyzerDbContext(dbContextOptions))
        {
            await setupContext.Database.EnsureCreatedAsync(CancellationToken.None);
        }

        var initialRate = CreateExchangeRate(
            sourceUpdatedAt: new DateTime(2026, 8, 6, 6, 4, 36),
            retrievedAtUtc: new DateTimeOffset(2026, 8, 6, 6, 5, 0, TimeSpan.Zero),
            cashChangeRate: 46.55073m);

        await using (var insertContext = new AssetValueAnalyzerDbContext(dbContextOptions))
        {
            var store = new EfExchangeRateStore(insertContext);

            var insertResult = await store.UpsertAsync(
                [initialRate],
                CancellationToken.None);

            Assert.Equal(1, insertResult.InsertedCount);
            Assert.Equal(0, insertResult.UpdatedCount);
            Assert.Equal(0, insertResult.UnchangedCount);
        }

        var refreshedRate = CreateExchangeRate(
            sourceUpdatedAt: new DateTime(2026, 8, 6, 10, 15, 0),
            retrievedAtUtc: new DateTimeOffset(2026, 8, 6, 10, 16, 0, TimeSpan.Zero),
            cashChangeRate: 47.12500m);

        await using (var updateContext = new AssetValueAnalyzerDbContext(dbContextOptions))
        {
            var store = new EfExchangeRateStore(updateContext);

            var updateResult = await store.UpsertAsync(
                [refreshedRate],
                CancellationToken.None);

            Assert.Equal(0, updateResult.InsertedCount);
            Assert.Equal(1, updateResult.UpdatedCount);
            Assert.Equal(0, updateResult.UnchangedCount);
        }

        await using var verificationContext = new AssetValueAnalyzerDbContext(dbContextOptions);
        var savedRates = await verificationContext.ExchangeRates
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);
        var savedRate = Assert.Single(savedRates);

        Assert.Equal(47.12500m, savedRate.CashChangeRate);
        Assert.Equal(new DateTime(2026, 8, 6, 10, 15, 0), savedRate.SourceUpdatedAt);
        Assert.Equal(
            new DateTimeOffset(2026, 8, 6, 10, 16, 0, TimeSpan.Zero),
            savedRate.RetrievedAtUtc);
    }

    [Fact]
    public async Task UpsertAsync_WithIdenticalRateValues_RefreshesRetrievalMetadata()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(CancellationToken.None);
        var dbContextOptions = new DbContextOptionsBuilder<AssetValueAnalyzerDbContext>()
            .UseSqlite(connection)
            .Options;
        var originalRetrievedAtUtc =
            new DateTimeOffset(2026, 8, 7, 6, 5, 0, TimeSpan.Zero);

        await using (var setupContext = new AssetValueAnalyzerDbContext(dbContextOptions))
        {
            await setupContext.Database.EnsureCreatedAsync(CancellationToken.None);
            setupContext.ExchangeRates.Add(CreateExchangeRate(
                sourceUpdatedAt: new DateTime(2026, 8, 7, 6, 4, 36),
                retrievedAtUtc: originalRetrievedAtUtc,
                cashChangeRate: 46.55073m));
            await setupContext.SaveChangesAsync(CancellationToken.None);
        }

        var refreshedSourceUpdatedAt = new DateTime(2026, 8, 7, 10, 15, 0);
        var refreshedRetrievedAtUtc =
            new DateTimeOffset(2026, 8, 7, 10, 16, 0, TimeSpan.Zero);
        var identicalRateFromLaterRequest = CreateExchangeRate(
            sourceUpdatedAt: refreshedSourceUpdatedAt,
            retrievedAtUtc: refreshedRetrievedAtUtc,
            cashChangeRate: 46.55073m);

        await using (var updateContext = new AssetValueAnalyzerDbContext(dbContextOptions))
        {
            var store = new EfExchangeRateStore(updateContext);

            var result = await store.UpsertAsync(
                [identicalRateFromLaterRequest],
                CancellationToken.None);

            Assert.Equal(0, result.InsertedCount);
            Assert.Equal(0, result.UpdatedCount);
            Assert.Equal(1, result.UnchangedCount);
        }

        await using var verificationContext = new AssetValueAnalyzerDbContext(dbContextOptions);
        var savedRate = await verificationContext.ExchangeRates
            .AsNoTracking()
            .SingleAsync(CancellationToken.None);

        Assert.NotEqual(originalRetrievedAtUtc, savedRate.RetrievedAtUtc);
        Assert.Equal(refreshedRetrievedAtUtc, savedRate.RetrievedAtUtc);
        Assert.Equal(refreshedSourceUpdatedAt, savedRate.SourceUpdatedAt);
    }

    private static ExchangeRate CreateExchangeRate(
        DateTime sourceUpdatedAt,
        DateTimeOffset retrievedAtUtc,
        decimal cashChangeRate) =>
        new(
            baseCurrencyCode: 1,
            foreignCurrencyCode: 56,
            sourceUpdatedAt,
            retrievedAtUtc,
            changeRate: 46.87830m,
            exchangeRateValue: 48.42890m,
            cashChangeRate,
            cashExchangeRate: 48.78997m,
            centralBankChangeRate: 47.48810m,
            centralBankExchangeRate: 47.57360m,
            crossRate: 1.00000m);
}
