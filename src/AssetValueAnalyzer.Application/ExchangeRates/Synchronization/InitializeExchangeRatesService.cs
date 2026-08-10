namespace AssetValueAnalyzer.Application.ExchangeRates.Synchronization;

public sealed class InitializeExchangeRatesService
{
    public const int MaximumInitialRateDelayDays = 10;

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

    public static DateOnly LatestAcceptableInitialRateDate { get; } =
        InitialBackfillDate.AddDays(MaximumInitialRateDelayDays);

    public async Task<InitializeExchangeRatesResult> InitializeAsync(
        CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);
        var coverage = await _exchangeRateStore.GetDateCoverageAsync(
            cancellationToken);
        var latestRateDate = coverage.LatestRateDate;

        if (latestRateDate > today)
        {
            throw new InvalidOperationException(
                "The latest stored exchange-rate date cannot be later than today.");
        }

        var request = CreateSynchronizationRequest(coverage, today);
        var synchronizationResult = await _synchronizationService.SynchronizeAsync(
            request,
            cancellationToken);

        return new InitializeExchangeRatesResult(
            latestRateDate,
            request.StartDate,
            request.EndDate,
            synchronizationResult);
    }

    private static SyncExchangeRatesRequest CreateSynchronizationRequest(
        ExchangeRateDateCoverage coverage,
        DateOnly today)
    {
        var latestRateDate = coverage.LatestRateDate;

        if (latestRateDate is null)
        {
            return new SyncExchangeRatesRequest(InitialBackfillDate, today);
        }

        if (coverage.EarliestRateDate is null ||
            coverage.EarliestRateDate > LatestAcceptableInitialRateDate)
        {
            return new SyncExchangeRatesRequest(InitialBackfillDate, today);
        }

        if (latestRateDate < today)
        {
            // Re-fetch the last stored day as a safe overlap. Upsert keeps this
            // idempotent and can repair a partially synchronized boundary day.
            return new SyncExchangeRatesRequest(latestRateDate, today);
        }

        // Omitting dates asks Finmaks for the current system day's latest rates.
        return new SyncExchangeRatesRequest();
    }
}
