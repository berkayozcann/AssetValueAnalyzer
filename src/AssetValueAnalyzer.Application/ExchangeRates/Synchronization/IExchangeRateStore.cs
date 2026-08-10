using AssetValueAnalyzer.Domain.ExchangeRates;

namespace AssetValueAnalyzer.Application.ExchangeRates.Synchronization;

public interface IExchangeRateStore
{
    Task<ExchangeRateDateCoverage> GetDateCoverageAsync(
        CancellationToken cancellationToken = default);

    Task<ExchangeRateUpsertResult> UpsertAsync(
        IReadOnlyCollection<ExchangeRate> exchangeRates,
        CancellationToken cancellationToken = default);
}

public sealed record ExchangeRateDateCoverage(
    DateOnly? EarliestRateDate,
    DateOnly? LatestRateDate);

public sealed record ExchangeRateUpsertResult(
    int InsertedCount,
    int UpdatedCount,
    int UnchangedCount);
