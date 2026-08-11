using AssetValueAnalyzer.Application.ExchangeRates.External;
using AssetValueAnalyzer.Application.ExchangeRates.Synchronization;
using AssetValueAnalyzer.Domain.ExchangeRates;
using AssetValueAnalyzer.Infrastructure.BackgroundJobs;
using Microsoft.Extensions.Logging;

namespace AssetValueAnalyzer.IntegrationTests.BackgroundJobs;

public sealed class ExchangeRateSynchronizationJobTests
{
    [Fact]
    public async Task ExecuteAsync_SynchronizesCurrentDayWithoutExplicitDateRange()
    {
        var utcNow = new DateTimeOffset(2026, 8, 9, 16, 30, 0, TimeSpan.Zero);
        var today = new DateOnly(2026, 8, 9);
        var client = new CapturingFinmaksClient();
        var store = new CapturingExchangeRateStore(
            new ExchangeRateBackfillState(today, utcNow.AddMinutes(-3)));
        var timeProvider = new FixedTimeProvider(utcNow);
        var synchronizationService = new ExchangeRateSynchronizationService(
            client,
            store,
            new InProcessExchangeRateSynchronizationLock(),
            timeProvider);
        var initializationService = new InitializeExchangeRatesService(
            store,
            synchronizationService,
            timeProvider);
        var notifier = new CapturingSynchronizationNotifier();
        var logger = new CapturingLogger<ExchangeRateSynchronizationJob>();
        var job = new ExchangeRateSynchronizationJob(
            initializationService,
            notifier,
            logger);

        await job.ExecuteAsync(CancellationToken.None);

        Assert.True(client.WasCalled);
        Assert.Null(client.StartDate);
        Assert.Null(client.EndDate);
        Assert.Equal(1, store.CallCount);
        Assert.Equal(1, notifier.CallCount);
        Assert.Contains(
            logger.Messages,
            message => message.Contains(
                "Current-day exchange rates are not available yet; " +
                "the next scheduled run will retry.",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_WhenHistoricalCheckpointIsMissing_RetriesBackfillInSameProcess()
    {
        var utcNow = new DateTimeOffset(2021, 12, 15, 16, 30, 0, TimeSpan.Zero);
        var today = new DateOnly(2021, 12, 15);
        var client = new CapturingFinmaksClient(CreateCompleteResponse);
        var store = new CapturingExchangeRateStore(state: null);
        var timeProvider = new FixedTimeProvider(utcNow);
        var synchronizationService = new ExchangeRateSynchronizationService(
            client,
            store,
            new InProcessExchangeRateSynchronizationLock(),
            timeProvider);
        var initializationService = new InitializeExchangeRatesService(
            store,
            synchronizationService,
            timeProvider);
        var notifier = new CapturingSynchronizationNotifier();
        var logger = new CapturingLogger<ExchangeRateSynchronizationJob>();
        var job = new ExchangeRateSynchronizationJob(
            initializationService,
            notifier,
            logger);

        await job.ExecuteAsync(CancellationToken.None);

        Assert.Equal(
            InitializeExchangeRatesService.InitialBackfillDate,
            client.StartDate);
        Assert.Equal(today, client.EndDate);
        Assert.Equal(today, store.MarkedCompletedThroughDate);
        Assert.Equal(1, notifier.CallCount);
    }

    private sealed class CapturingFinmaksClient(
        Func<DateOnly?, DateOnly?, IReadOnlyList<ExchangeRateQuote>>? responseFactory = null)
        : IFinmaksExchangeRateClient
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

            IReadOnlyList<ExchangeRateQuote> result = responseFactory is null
                ? []
                : responseFactory(startDate, endDate);

            return Task.FromResult(result);
        }
    }

    private sealed class CapturingExchangeRateStore(
        ExchangeRateBackfillState? state) : IExchangeRateStore
    {
        public int CallCount { get; private set; }

        public DateOnly? MarkedCompletedThroughDate { get; private set; }

        public Task<ExchangeRateBackfillState?> GetBackfillStateAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(state);

        public Task MarkBackfillCompletedAsync(
            DateOnly completedThroughDate,
            DateTimeOffset completedAtUtc,
            CancellationToken cancellationToken = default)
        {
            MarkedCompletedThroughDate = completedThroughDate;
            return Task.CompletedTask;
        }

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

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }

    private static IReadOnlyList<ExchangeRateQuote> CreateCompleteResponse(
        DateOnly? startDate,
        DateOnly? endDate)
    {
        Assert.True(startDate.HasValue);
        Assert.True(endDate.HasValue);

        var quotes = new List<ExchangeRateQuote>();

        for (var date = startDate.Value; date <= endDate.Value; date = date.AddDays(1))
        {
            quotes.Add(
                new ExchangeRateQuote(
                    BaseCurrencyCode: 1,
                    ForeignCurrencyCode: 56,
                    ChangeRate: 45m,
                    ExchangeRateValue: 46m,
                    CashChangeRate: 45.5m,
                    CashExchangeRate: 46.5m,
                    CentralBankChangeRate: 45.2m,
                    CentralBankExchangeRate: 45.8m,
                    CrossRate: 1m,
                    SourceUpdatedAt: date.ToDateTime(new TimeOnly(8, 0))));
        }

        return quotes;
    }
}
