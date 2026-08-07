using AssetValueAnalyzer.Application.ExchangeRates.External;
using AssetValueAnalyzer.Application.ExchangeRates.Synchronization;
using AssetValueAnalyzer.Domain.ExchangeRates;

namespace AssetValueAnalyzer.UnitTests.ExchangeRates;

public sealed class InitializeExchangeRatesServiceTests
{
    private static readonly DateTimeOffset UtcNow =
        new(2026, 8, 7, 9, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task InitializeAsync_WithEmptyStore_BackfillsFromDecember2021()
    {
        var fixture = new Fixture(latestRateDate: null);

        var result = await fixture.Service.InitializeAsync(CancellationToken.None);

        Assert.Null(result.PreviouslyLatestRateDate);
        Assert.Equal(InitializeExchangeRatesService.InitialBackfillDate, result.RequestedStartDate);
        Assert.Equal(new DateOnly(2026, 8, 7), result.RequestedEndDate);
        Assert.Equal(result.RequestedStartDate, fixture.Client.StartDate);
        Assert.Equal(result.RequestedEndDate, fixture.Client.EndDate);
    }

    [Fact]
    public async Task InitializeAsync_WithOlderData_OverlapsLatestDayAndFillsToToday()
    {
        var latestRateDate = new DateOnly(2026, 8, 5);
        var fixture = new Fixture(latestRateDate);

        var result = await fixture.Service.InitializeAsync(CancellationToken.None);

        Assert.Equal(latestRateDate, result.PreviouslyLatestRateDate);
        Assert.Equal(latestRateDate, result.RequestedStartDate);
        Assert.Equal(new DateOnly(2026, 8, 7), result.RequestedEndDate);
    }

    [Fact]
    public async Task InitializeAsync_WithTodaysData_RequestsCurrentRatesWithoutDates()
    {
        var fixture = new Fixture(new DateOnly(2026, 8, 7));

        var result = await fixture.Service.InitializeAsync(CancellationToken.None);

        Assert.Null(result.RequestedStartDate);
        Assert.Null(result.RequestedEndDate);
        Assert.Null(fixture.Client.StartDate);
        Assert.Null(fixture.Client.EndDate);
    }

    [Fact]
    public async Task InitializeAsync_WithFutureStoredDate_RejectsBeforeCallingFinmaks()
    {
        var fixture = new Fixture(new DateOnly(2026, 8, 8));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Service.InitializeAsync(CancellationToken.None));

        Assert.False(fixture.Client.WasCalled);
    }

    private sealed class Fixture
    {
        public Fixture(DateOnly? latestRateDate)
        {
            Client = new CapturingFinmaksClient();
            var store = new StubExchangeRateStore(latestRateDate);
            var timeProvider = new FixedTimeProvider(UtcNow);
            var synchronizationService = new ExchangeRateSynchronizationService(
                Client,
                store,
                timeProvider);

            Service = new InitializeExchangeRatesService(
                store,
                synchronizationService,
                timeProvider);
        }

        public CapturingFinmaksClient Client { get; }

        public InitializeExchangeRatesService Service { get; }
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

    private sealed class StubExchangeRateStore(DateOnly? latestRateDate) : IExchangeRateStore
    {
        public Task<DateOnly?> GetLatestRateDateAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(latestRateDate);

        public Task<ExchangeRateUpsertResult> UpsertAsync(
            IReadOnlyCollection<ExchangeRate> exchangeRates,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ExchangeRateUpsertResult(0, 0));
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
