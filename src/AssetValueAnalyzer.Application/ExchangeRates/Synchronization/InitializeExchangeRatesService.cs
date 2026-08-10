namespace AssetValueAnalyzer.Application.ExchangeRates.Synchronization;

public sealed class InitializeExchangeRatesService
{
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

        var request = CreateSynchronizationRequest(completedThroughDate, today);
        var synchronizationResult = await _synchronizationService.SynchronizeAsync(
            request,
            cancellationToken);

        if (request.StartDate.HasValue && synchronizationResult.ReceivedCount == 0)
        {
            throw new InvalidOperationException(
                "The ranged exchange-rate synchronization returned no data; " +
                "the all-currency backfill checkpoint was not advanced.");
        }

        await _exchangeRateStore.MarkBackfillCompletedAsync(
            today,
            _timeProvider.GetUtcNow(),
            cancellationToken);

        return new InitializeExchangeRatesResult(
            completedThroughDate,
            request.StartDate,
            request.EndDate,
            synchronizationResult);
    }

    private static SyncExchangeRatesRequest CreateSynchronizationRequest(
        DateOnly? completedThroughDate,
        DateOnly today)
    {
        if (completedThroughDate is null)
        {
            return new SyncExchangeRatesRequest(InitialBackfillDate, today);
        }

        if (completedThroughDate < today)
        {
            // Re-fetch the last completed day as a safe overlap. The request
            // contains every currency pair returned by Finmaks for the range.
            return new SyncExchangeRatesRequest(completedThroughDate, today);
        }

        // Omitting dates asks Finmaks for the current system day's latest rates.
        return new SyncExchangeRatesRequest();
    }
}
