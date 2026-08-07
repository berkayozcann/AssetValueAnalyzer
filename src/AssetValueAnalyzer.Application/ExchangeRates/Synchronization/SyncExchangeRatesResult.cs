namespace AssetValueAnalyzer.Application.ExchangeRates.Synchronization;

public sealed record SyncExchangeRatesResult(
    int ReceivedCount,
    int InsertedCount,
    int UpdatedCount,
    int UnchangedCount);
