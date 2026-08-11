namespace AssetValueAnalyzer.Application.ExchangeRates.Synchronization;

public sealed class InitializeExchangeRatesService
{
    private const int BackfillChunkYears = 1;
    private const int MaximumExpectedRateGapInDays = 10;

    private readonly IExchangeRateStore _exchangeRateStore;
    private readonly ExchangeRateSynchronizationService _synchronizationService;
    private readonly TimeProvider _timeProvider;

    public InitializeExchangeRatesService(
        IExchangeRateStore exchangeRateStore,
        ExchangeRateSynchronizationService synchronizationService,
        TimeProvider timeProvider)
    {
        _exchangeRateStore = exchangeRateStore;
        _synchronizationService = synchronizationService;
        _timeProvider = timeProvider;
    }

    public static DateOnly InitialBackfillDate { get; } = new(2021, 12, 1);

    public async Task<InitializeExchangeRatesResult> InitializeAsync(
        CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);
        var backfillState = await _exchangeRateStore.GetBackfillStateAsync(
            cancellationToken);
        var completedThroughDate = backfillState?.CompletedThroughDate;

        if (completedThroughDate > today)
        {
            throw new InvalidOperationException(
                "The completed exchange-rate backfill date cannot be later than today.");
        }

        if (completedThroughDate == today)
        {
            var currentDayResult = await _synchronizationService.SynchronizeAsync(
                new SyncExchangeRatesRequest(),
                cancellationToken);

            await _exchangeRateStore.MarkBackfillCompletedAsync(
                today,
                _timeProvider.GetUtcNow(),
                cancellationToken);

            return new InitializeExchangeRatesResult(
                completedThroughDate,
                RequestedStartDate: null,
                RequestedEndDate: null,
                currentDayResult);
        }

        var firstRequestedDate = completedThroughDate ?? InitialBackfillDate;
        var chunkStartDate = firstRequestedDate;
        var aggregateResult = EmptySynchronizationResult();

        while (chunkStartDate < today)
        {
            var chunkEndDate = Min(chunkStartDate.AddYears(BackfillChunkYears), today);
            var request = new SyncExchangeRatesRequest(chunkStartDate, chunkEndDate);
            var chunkResult = await _synchronizationService.SynchronizeAsync(
                request,
                cancellationToken);

            ValidateChunkCoverage(chunkStartDate, chunkEndDate, chunkResult);

            await _exchangeRateStore.MarkBackfillCompletedAsync(
                chunkEndDate,
                _timeProvider.GetUtcNow(),
                cancellationToken);

            aggregateResult = Combine(aggregateResult, chunkResult);
            chunkStartDate = chunkEndDate;
        }

        return new InitializeExchangeRatesResult(
            completedThroughDate,
            firstRequestedDate,
            today,
            aggregateResult);
    }

    private static void ValidateChunkCoverage(
        DateOnly startDate,
        DateOnly endDate,
        SyncExchangeRatesResult synchronizationResult)
    {
        if (synchronizationResult.ReceivedCount == 0)
        {
            throw new InvalidOperationException(
                "The ranged exchange-rate synchronization returned no data; " +
                "the all-currency backfill checkpoint was not advanced.");
        }

        var rateDates = synchronizationResult.ReceivedRateDates;

        if (rateDates.Count == 0 ||
            rateDates[0] < startDate ||
            rateDates[^1] > endDate)
        {
            throw new InvalidOperationException(
                "The ranged exchange-rate synchronization returned dates outside " +
                "the requested chunk; the all-currency backfill checkpoint was not advanced.");
        }

        var previousDate = startDate;

        foreach (var rateDate in rateDates)
        {
            EnsureExpectedGap(previousDate, rateDate);
            previousDate = rateDate;
        }

        EnsureExpectedGap(previousDate, endDate);
    }

    private static void EnsureExpectedGap(DateOnly previousDate, DateOnly nextDate)
    {
        if (nextDate.DayNumber - previousDate.DayNumber <= MaximumExpectedRateGapInDays)
        {
            return;
        }

        throw new InvalidOperationException(
            "The ranged exchange-rate synchronization returned incomplete date coverage; " +
            "the all-currency backfill checkpoint was not advanced.");
    }

    private static SyncExchangeRatesResult EmptySynchronizationResult() =>
        new(0, 0, 0, 0, []);

    private static SyncExchangeRatesResult Combine(
        SyncExchangeRatesResult aggregate,
        SyncExchangeRatesResult current) =>
        new(
            aggregate.ReceivedCount + current.ReceivedCount,
            aggregate.InsertedCount + current.InsertedCount,
            aggregate.UpdatedCount + current.UpdatedCount,
            aggregate.UnchangedCount + current.UnchangedCount,
            aggregate.ReceivedRateDates
                .Concat(current.ReceivedRateDates)
                .Distinct()
                .Order()
                .ToArray());

    private static DateOnly Min(DateOnly first, DateOnly second) =>
        first <= second ? first : second;
}
