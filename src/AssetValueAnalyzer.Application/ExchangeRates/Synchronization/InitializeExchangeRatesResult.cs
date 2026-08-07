namespace AssetValueAnalyzer.Application.ExchangeRates.Synchronization;

public sealed record InitializeExchangeRatesResult(
    DateOnly? PreviouslyLatestRateDate,
    DateOnly? RequestedStartDate,
    DateOnly? RequestedEndDate,
    SyncExchangeRatesResult Synchronization);
