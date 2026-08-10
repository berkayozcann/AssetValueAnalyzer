using AssetValueAnalyzer.Application.ExchangeRates.External;
using AssetValueAnalyzer.Application.ExchangeRates.Synchronization;
using AssetValueAnalyzer.Domain.ExchangeRates;
using AssetValueAnalyzer.Infrastructure.BackgroundJobs;
using Microsoft.Extensions.Logging.Abstractions;

namespace AssetValueAnalyzer.IntegrationTests.BackgroundJobs;

public sealed class ExchangeRateSynchronizationJobTests
{
    [Fact]
    public async Task ExecuteAsync_SynchronizesCurrentDayWithoutExplicitDateRange()
    {
        var client = new CapturingFinmaksClient();
        var store = new CapturingExchangeRateStore();
        var synchronizationService = new ExchangeRateSynchronizationService(
            client,
            store,
            new FixedTimeProvider(
                new DateTimeOffset(2026, 8, 9, 16, 30, 0, TimeSpan.Zero)));
        var notifier = new CapturingSynchronizationNotifier();
        var job = new ExchangeRateSynchronizationJob(
            synchronizationService,
            notifier,
            NullLogger<ExchangeRateSynchronizationJob>.Instance);

        await job.ExecuteAsync(CancellationToken.None);

        Assert.True(client.WasCalled);
        Assert.Null(client.StartDate);
        Assert.Null(client.EndDate);
        Assert.Equal(1, store.CallCount);
        Assert.Equal(1, notifier.CallCount);
    }

    private sealed class CapturingFinmaksClient : IFinmaksExchangeRateClient
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

            return Task.FromResult<IReadOnlyList<ExchangeRateQuote>>([]);
        }
    }

    private sealed class CapturingExchangeRateStore : IExchangeRateStore
    {
        public int CallCount { get; private set; }

        public Task<ExchangeRateDateCoverage> GetDateCoverageAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ExchangeRateDateCoverage(null, null));

        public Task<ExchangeRateUpsertResult> UpsertAsync(
            IReadOnlyCollection<ExchangeRate> exchangeRates,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(new ExchangeRateUpsertResult(0, 0, 0));
        }
    }

    private sealed class CapturingSynchronizationNotifier
        : IExchangeRateSynchronizationNotifier
    {
        public int CallCount { get; private set; }

        public Task NotifyCompletedAsync(
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
