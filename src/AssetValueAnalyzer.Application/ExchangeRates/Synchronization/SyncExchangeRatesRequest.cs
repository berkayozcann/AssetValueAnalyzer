namespace AssetValueAnalyzer.Application.ExchangeRates.Synchronization;

public sealed record SyncExchangeRatesRequest(
    DateOnly? StartDate = null,
    DateOnly? EndDate = null);
