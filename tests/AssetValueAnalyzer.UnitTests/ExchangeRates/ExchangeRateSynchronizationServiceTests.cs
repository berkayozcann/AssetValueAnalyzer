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
        var eurQuote = quote with
        {
            BaseCurrencyCode = 4,
            CashChangeRate = 53.12500m
        };
        var finmaksClient = new StubFinmaksExchangeRateClient([quote, eurQuote]);
        var exchangeRateStore = new CapturingExchangeRateStore(
            new ExchangeRateUpsertResult(
                InsertedCount: 2,
                UpdatedCount: 0,
                UnchangedCount: 0));
        var service = new ExchangeRateSynchronizationService(
            finmaksClient,
            exchangeRateStore,
            new InProcessExchangeRateSynchronizationLock(),
            new FixedTimeProvider(retrievedAtUtc));
        var request = new SyncExchangeRatesRequest(
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 6));

        var result = await service.SynchronizeAsync(request, CancellationToken.None);

        Assert.Equal(2, result.ReceivedCount);
        Assert.Equal(2, result.InsertedCount);
        Assert.Equal(0, result.UpdatedCount);
        Assert.Equal(0, result.UnchangedCount);
        Assert.Equal(request.StartDate, finmaksClient.StartDate);
        Assert.Equal(request.EndDate, finmaksClient.EndDate);

        Assert.Collection(
            exchangeRateStore.ExchangeRates,
            savedRate =>
            {
                Assert.Equal(1, savedRate.BaseCurrencyCode);
                Assert.Equal(56, savedRate.ForeignCurrencyCode);
                Assert.Equal(new DateOnly(2026, 8, 6), savedRate.RateDate);
                Assert.Equal(46.55073m, savedRate.CashChangeRate);
                Assert.Equal(retrievedAtUtc, savedRate.RetrievedAtUtc);
            },
            savedRate =>
            {
                Assert.Equal(4, savedRate.BaseCurrencyCode);
                Assert.Equal(56, savedRate.ForeignCurrencyCode);
                Assert.Equal(53.12500m, savedRate.CashChangeRate);
                Assert.Equal(retrievedAtUtc, savedRate.RetrievedAtUtc);
            });
    }

    [Fact]
    public async Task SynchronizeAsync_WithIncompleteDateRange_RejectsBeforeCallingClient()
    {
        var finmaksClient = new StubFinmaksExchangeRateClient([]);
        var service = new ExchangeRateSynchronizationService(
            finmaksClient,
            new CapturingExchangeRateStore(new ExchangeRateUpsertResult(0, 0, 0)),
            new InProcessExchangeRateSynchronizationLock(),
            new FixedTimeProvider(DateTimeOffset.UnixEpoch));
        var request = new SyncExchangeRatesRequest(
            StartDate: new DateOnly(2026, 8, 1),
            EndDate: null);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.SynchronizeAsync(request, CancellationToken.None));

        Assert.False(finmaksClient.WasCalled);
    }

    [Fact]
    public async Task SynchronizeAsync_ConcurrentScopesWithSharedLock_AreSerialized()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var client = new BlockingFinmaksExchangeRateClient();
        var synchronizationLock = new InProcessExchangeRateSynchronizationLock();
        var firstService = CreateService(client, synchronizationLock);
        var secondService = CreateService(client, synchronizationLock);

        var firstSynchronization = firstService.SynchronizeAsync(
            new SyncExchangeRatesRequest(),
            timeout.Token);
        await client.FirstCallStarted.WaitAsync(timeout.Token);

        var secondSynchronization = secondService.SynchronizeAsync(
            new SyncExchangeRatesRequest(),
            timeout.Token);

        try
        {
            var completedBeforeRelease = await Task.WhenAny(
                client.SecondCallStarted,
                Task.Delay(TimeSpan.FromMilliseconds(150), timeout.Token));

            Assert.NotSame(client.SecondCallStarted, completedBeforeRelease);
            Assert.Equal(1, client.CallCount);
        }
        finally
        {
            client.ReleaseFirstCall();
        }

        await Task.WhenAll(firstSynchronization, secondSynchronization);

        Assert.Equal(2, client.CallCount);
        Assert.Equal(1, client.MaximumConcurrentCallCount);
    }

    private static ExchangeRateSynchronizationService CreateService(
        IFinmaksExchangeRateClient client,
        IExchangeRateSynchronizationLock synchronizationLock) =>
        new(
            client,
            new CapturingExchangeRateStore(new ExchangeRateUpsertResult(0, 0, 0)),
            synchronizationLock,
            new FixedTimeProvider(DateTimeOffset.UnixEpoch));

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

    private sealed class BlockingFinmaksExchangeRateClient
        : IFinmaksExchangeRateClient
    {
        private readonly TaskCompletionSource _firstCallStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _releaseFirstCall =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _secondCallStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _activeCallCount;
        private int _callCount;
        private int _maximumConcurrentCallCount;

        public Task FirstCallStarted => _firstCallStarted.Task;

        public Task SecondCallStarted => _secondCallStarted.Task;

        public int CallCount => Volatile.Read(ref _callCount);

        public int MaximumConcurrentCallCount =>
            Volatile.Read(ref _maximumConcurrentCallCount);

        public async Task<IReadOnlyList<ExchangeRateQuote>> GetRatesAsync(
            DateOnly? startDate = null,
            DateOnly? endDate = null,
            CancellationToken cancellationToken = default)
        {
            var callNumber = Interlocked.Increment(ref _callCount);
            var activeCallCount = Interlocked.Increment(ref _activeCallCount);
            UpdateMaximumConcurrentCallCount(activeCallCount);

            try
            {
                if (callNumber == 1)
                {
                    _firstCallStarted.TrySetResult();
                    await _releaseFirstCall.Task.WaitAsync(cancellationToken);
                }
                else
                {
                    _secondCallStarted.TrySetResult();
                }

                return [];
            }
            finally
            {
                Interlocked.Decrement(ref _activeCallCount);
            }
        }

        public void ReleaseFirstCall() => _releaseFirstCall.TrySetResult();

        private void UpdateMaximumConcurrentCallCount(int candidate)
        {
            var current = Volatile.Read(ref _maximumConcurrentCallCount);

            while (candidate > current)
            {
                var observed = Interlocked.CompareExchange(
                    ref _maximumConcurrentCallCount,
                    candidate,
                    current);

                if (observed == current)
                {
                    return;
                }

                current = observed;
            }
        }
    }

    private sealed class CapturingExchangeRateStore(
        ExchangeRateUpsertResult result) : IExchangeRateStore
    {
        public IReadOnlyCollection<ExchangeRate> ExchangeRates { get; private set; } = [];

        public Task<ExchangeRateBackfillState?> GetBackfillStateAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ExchangeRateBackfillState?>(null);

        public Task MarkBackfillCompletedAsync(
            DateOnly completedThroughDate,
            DateTimeOffset completedAtUtc,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

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
