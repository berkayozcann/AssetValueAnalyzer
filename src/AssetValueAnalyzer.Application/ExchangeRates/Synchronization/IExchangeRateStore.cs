using AssetValueAnalyzer.Domain.ExchangeRates;

namespace AssetValueAnalyzer.Application.ExchangeRates.Synchronization;

public interface IExchangeRateStore
{
    Task<ExchangeRateUpsertResult> UpsertAsync(
        IReadOnlyCollection<ExchangeRate> exchangeRates,
        CancellationToken cancellationToken = default);
}

public sealed record ExchangeRateUpsertResult(
    int InsertedCount,
    int UpdatedCount);
