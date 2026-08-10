namespace AssetValueAnalyzer.Application.ExchangeRates.Synchronization;

public sealed record InitializeExchangeRatesResult(
    DateOnly? PreviouslyCompletedThroughDate,
    DateOnly? RequestedStartDate,
    DateOnly? RequestedEndDate,
    SyncExchangeRatesResult Synchronization);
