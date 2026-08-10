using AssetValueAnalyzer.Application.ExchangeRates.External;
using AssetValueAnalyzer.Application.ExchangeRates.Synchronization;
using AssetValueAnalyzer.Domain.ExchangeRates;

namespace AssetValueAnalyzer.UnitTests.ExchangeRates;

public sealed class InitializeExchangeRatesServiceTests
{
    private static readonly DateTimeOffset UtcNow =
        new(2026, 8, 7, 9, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task InitializeAsync_WithoutBackfillCheckpoint_RequestsAllRatesFromDecember2021()
    {
        var fixture = new Fixture(completedThroughDate: null);

        var result = await fixture.Service.InitializeAsync(CancellationToken.None);

        Assert.Null(result.PreviouslyCompletedThroughDate);
        Assert.Equal(
            InitializeExchangeRatesService.InitialBackfillDate,
            result.RequestedStartDate);
        Assert.Equal(new DateOnly(2026, 8, 7), result.RequestedEndDate);
        Assert.Equal(result.RequestedStartDate, fixture.Client.StartDate);
        Assert.Equal(result.RequestedEndDate, fixture.Client.EndDate);
        Assert.Equal(new DateOnly(2026, 8, 7), fixture.Store.MarkedCompletedThroughDate);
        Assert.Equal(UtcNow, fixture.Store.MarkedCompletedAtUtc);
    }

    [Fact]
    public async Task InitializeAsync_WithOlderCompletedCheckpoint_OverlapsLastCompletedDayAndFillsToToday()
    {
        var completedThroughDate = new DateOnly(2026, 8, 5);
        var fixture = new Fixture(completedThroughDate);

        var result = await fixture.Service.InitializeAsync(CancellationToken.None);

        Assert.Equal(completedThroughDate, result.PreviouslyCompletedThroughDate);
        Assert.Equal(completedThroughDate, result.RequestedStartDate);
        Assert.Equal(new DateOnly(2026, 8, 7), result.RequestedEndDate);
        Assert.Equal(new DateOnly(2026, 8, 7), fixture.Store.MarkedCompletedThroughDate);
    }

    [Fact]
    public async Task InitializeAsync_WithTodaysCompletedCheckpoint_RequestsCurrentRatesWithoutDates()
    {
        var today = new DateOnly(2026, 8, 7);
        var fixture = new Fixture(today);

        var result = await fixture.Service.InitializeAsync(CancellationToken.None);

        Assert.Equal(today, result.PreviouslyCompletedThroughDate);
        Assert.Null(result.RequestedStartDate);
        Assert.Null(result.RequestedEndDate);
        Assert.Null(fixture.Client.StartDate);
        Assert.Null(fixture.Client.EndDate);
        Assert.Equal(today, fixture.Store.MarkedCompletedThroughDate);
    }

    [Fact]
    public async Task InitializeAsync_WithFutureCompletedCheckpoint_RejectsBeforeCallingFinmaks()
    {
        var fixture = new Fixture(new DateOnly(2026, 8, 8));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Service.InitializeAsync(CancellationToken.None));

        Assert.False(fixture.Client.WasCalled);
        Assert.Null(fixture.Store.MarkedCompletedThroughDate);
    }

    [Fact]
    public async Task InitializeAsync_WhenSynchronizationFails_DoesNotAdvanceCheckpoint()
    {
        var fixture = new Fixture(
            completedThroughDate: null,
            clientException: new HttpRequestException("Controlled Finmaks failure."));

        await Assert.ThrowsAsync<HttpRequestException>(
            () => fixture.Service.InitializeAsync(CancellationToken.None));

        Assert.Null(fixture.Store.MarkedCompletedThroughDate);
    }

    [Fact]
    public async Task InitializeAsync_WhenRangedSynchronizationReturnsNoRates_DoesNotAdvanceCheckpoint()
    {
        var fixture = new Fixture(
            completedThroughDate: null,
            returnEmptyResponse: true);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Service.InitializeAsync(CancellationToken.None));

        Assert.Contains("checkpoint was not advanced", exception.Message);
        Assert.Null(fixture.Store.MarkedCompletedThroughDate);
    }

    private sealed class Fixture
    {
        public Fixture(
            DateOnly? completedThroughDate,
            Exception? clientException = null,
            bool returnEmptyResponse = false)
        {
            Client = new CapturingFinmaksClient(clientException, returnEmptyResponse);
            Store = new StubExchangeRateStore(
                completedThroughDate is null
                    ? null
                    : new ExchangeRateBackfillState(
                        completedThroughDate.Value,
                        UtcNow.AddDays(-1)));
            var timeProvider = new FixedTimeProvider(UtcNow);
            var synchronizationService = new ExchangeRateSynchronizationService(
                Client,
                Store,
                timeProvider);

            Service = new InitializeExchangeRatesService(
                Store,
                synchronizationService,
                timeProvider);
        }

        public CapturingFinmaksClient Client { get; }

        public StubExchangeRateStore Store { get; }

        public InitializeExchangeRatesService Service { get; }
    }

    private sealed class CapturingFinmaksClient(
        Exception? exception = null,
        bool returnEmptyResponse = false) : IFinmaksExchangeRateClient
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

            if (exception is not null)
            {
                return Task.FromException<IReadOnlyList<ExchangeRateQuote>>(exception);
            }

            IReadOnlyList<ExchangeRateQuote> result = returnEmptyResponse
                ? []
                :
                [
                    new(
                        BaseCurrencyCode: 1,
                        ForeignCurrencyCode: 56,
                        ChangeRate: 45m,
                        ExchangeRateValue: 46m,
                        CashChangeRate: 45.5m,
                        CashExchangeRate: 46.5m,
                        CentralBankChangeRate: 45.2m,
                        CentralBankExchangeRate: 45.8m,
                        CrossRate: 1m,
                        SourceUpdatedAt: new DateTime(2026, 8, 7, 8, 0, 0))
                ];

            return Task.FromResult(result);
        }
    }

    private sealed class StubExchangeRateStore(
        ExchangeRateBackfillState? state) : IExchangeRateStore
    {
        public DateOnly? MarkedCompletedThroughDate { get; private set; }

        public DateTimeOffset? MarkedCompletedAtUtc { get; private set; }

        public Task<ExchangeRateBackfillState?> GetBackfillStateAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(state);

        public Task MarkBackfillCompletedAsync(
            DateOnly completedThroughDate,
            DateTimeOffset completedAtUtc,
            CancellationToken cancellationToken = default)
        {
            MarkedCompletedThroughDate = completedThroughDate;
            MarkedCompletedAtUtc = completedAtUtc;
            return Task.CompletedTask;
        }

        public Task<ExchangeRateUpsertResult> UpsertAsync(
            IReadOnlyCollection<ExchangeRate> exchangeRates,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ExchangeRateUpsertResult(0, 0, 0));
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
