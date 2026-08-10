using AssetValueAnalyzer.Domain.ExchangeRates;

namespace AssetValueAnalyzer.Application.ExchangeRates.Synchronization;

public interface IExchangeRateStore
{
    Task<ExchangeRateBackfillState?> GetBackfillStateAsync(
        CancellationToken cancellationToken = default);

    Task MarkBackfillCompletedAsync(
        DateOnly completedThroughDate,
        DateTimeOffset completedAtUtc,
        CancellationToken cancellationToken = default);

    Task<ExchangeRateUpsertResult> UpsertAsync(
        IReadOnlyCollection<ExchangeRate> exchangeRates,
        CancellationToken cancellationToken = default);
}

public sealed record ExchangeRateBackfillState(
    DateOnly CompletedThroughDate,
    DateTimeOffset CompletedAtUtc);

public sealed record ExchangeRateUpsertResult(
    int InsertedCount,
    int UpdatedCount,
    int UnchangedCount);
