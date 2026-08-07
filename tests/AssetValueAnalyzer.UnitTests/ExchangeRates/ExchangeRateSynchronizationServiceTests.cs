using AssetValueAnalyzer.Application.ExchangeRates.External;
using AssetValueAnalyzer.Application.ExchangeRates.Synchronization;
using AssetValueAnalyzer.Domain.ExchangeRates;

namespace AssetValueAnalyzer.UnitTests.ExchangeRates;

public sealed class ExchangeRateSynchronizationServiceTests
{
    [Fact]
    public async Task SynchronizeAsync_MapsQuotesAndReturnsPersistenceCounts()
    {
        var retrievedAtUtc = new DateTimeOffset(2026, 8, 7, 9, 30, 0, TimeSpan.Zero);
        var quote = new ExchangeRateQuote(
            BaseCurrencyCode: 1,
            ForeignCurrencyCode: 56,
            ChangeRate: 46.87830m,
            ExchangeRateValue: 48.42890m,
            CashChangeRate: 46.55073m,
            CashExchangeRate: 48.78997m,
            CentralBankChangeRate: 47.48810m,
            CentralBankExchangeRate: 47.57360m,
            CrossRate: 1.00000m,
            SourceUpdatedAt: new DateTime(2026, 8, 6, 6, 4, 36));
        var finmaksClient = new StubFinmaksExchangeRateClient([quote]);
        var exchangeRateStore = new CapturingExchangeRateStore(
            new ExchangeRateUpsertResult(InsertedCount: 1, UpdatedCount: 0));
        var service = new ExchangeRateSynchronizationService(
            finmaksClient,
            exchangeRateStore,
            new FixedTimeProvider(retrievedAtUtc));
        var request = new SyncExchangeRatesRequest(
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 6));

        var result = await service.SynchronizeAsync(request, CancellationToken.None);

        Assert.Equal(1, result.ReceivedCount);
        Assert.Equal(1, result.InsertedCount);
        Assert.Equal(0, result.UpdatedCount);
        Assert.Equal(request.StartDate, finmaksClient.StartDate);
        Assert.Equal(request.EndDate, finmaksClient.EndDate);

        var savedRate = Assert.Single(exchangeRateStore.ExchangeRates);
        Assert.Equal(1, savedRate.BaseCurrencyCode);
        Assert.Equal(56, savedRate.ForeignCurrencyCode);
        Assert.Equal(new DateOnly(2026, 8, 6), savedRate.RateDate);
        Assert.Equal(46.55073m, savedRate.CashChangeRate);
        Assert.Equal(retrievedAtUtc, savedRate.RetrievedAtUtc);
    }

    [Fact]
    public async Task SynchronizeAsync_WithIncompleteDateRange_RejectsBeforeCallingClient()
    {
        var finmaksClient = new StubFinmaksExchangeRateClient([]);
        var service = new ExchangeRateSynchronizationService(
            finmaksClient,
            new CapturingExchangeRateStore(new ExchangeRateUpsertResult(0, 0)),
            new FixedTimeProvider(DateTimeOffset.UnixEpoch));
        var request = new SyncExchangeRatesRequest(
            StartDate: new DateOnly(2026, 8, 1),
            EndDate: null);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.SynchronizeAsync(request, CancellationToken.None));

        Assert.False(finmaksClient.WasCalled);
    }

    private sealed class StubFinmaksExchangeRateClient(
        IReadOnlyList<ExchangeRateQuote> quotes) : IFinmaksExchangeRateClient
    {
        public bool WasCalled { get; private set; }

        public DateOnly? StartDate { get; private set; }

        public DateOnly? EndDate { get; private set; }

        public Task<IReadOnlyList<ExchangeRateQuote>> GetRatesAsync(
            DateOnly? startDate = null,
            DateOnly? endDate = null,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            StartDate = startDate;
            EndDate = endDate;

            return Task.FromResult(quotes);
        }
    }

    private sealed class CapturingExchangeRateStore(
        ExchangeRateUpsertResult result) : IExchangeRateStore
    {
        public IReadOnlyCollection<ExchangeRate> ExchangeRates { get; private set; } = [];

        public Task<ExchangeRateUpsertResult> UpsertAsync(
            IReadOnlyCollection<ExchangeRate> exchangeRates,
            CancellationToken cancellationToken = default)
        {
            ExchangeRates = exchangeRates;

            return Task.FromResult(result);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
