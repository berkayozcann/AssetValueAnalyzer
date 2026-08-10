using AssetValueAnalyzer.Application.ExchangeRates.Synchronization;
using AssetValueAnalyzer.Domain.ExchangeRates;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AssetValueAnalyzer.Infrastructure.Persistence.ExchangeRates;

public sealed class EfExchangeRateStore(
    AssetValueAnalyzerDbContext dbContext) : IExchangeRateStore
{
    public async Task<ExchangeRateBackfillState?> GetBackfillStateAsync(
        CancellationToken cancellationToken = default)
    {
        return await dbContext.ExchangeRateBackfillCheckpoints
            .AsNoTracking()
            .Select(checkpoint => new ExchangeRateBackfillState(
                checkpoint.CompletedThroughDate,
                checkpoint.CompletedAtUtc))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task MarkBackfillCompletedAsync(
        DateOnly completedThroughDate,
        DateTimeOffset completedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var checkpoint = await dbContext.ExchangeRateBackfillCheckpoints
            .SingleOrDefaultAsync(cancellationToken);

        if (checkpoint is null)
        {
            dbContext.ExchangeRateBackfillCheckpoints.Add(
                new ExchangeRateBackfillCheckpoint(
                    completedThroughDate,
                    completedAtUtc));
        }
        else
        {
            checkpoint.MarkCompleted(completedThroughDate, completedAtUtc);
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (IsSqlServerUniqueConstraintViolation(exception))
        {
            // A second application instance created the singleton checkpoint.
            // Re-read it and advance it exactly once without regressing the date.
            dbContext.ChangeTracker.Clear();
            checkpoint = await dbContext.ExchangeRateBackfillCheckpoints
                .SingleAsync(cancellationToken);
            checkpoint.MarkCompleted(completedThroughDate, completedAtUtc);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<ExchangeRateUpsertResult> UpsertAsync(
        IReadOnlyCollection<ExchangeRate> exchangeRates,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(exchangeRates);

        if (exchangeRates.Count == 0)
        {
            return new ExchangeRateUpsertResult(0, 0, 0);
        }

        var incomingRates = exchangeRates
            .GroupBy(CreateKey)
            .Select(group => group.MaxBy(rate => rate.SourceUpdatedAt)!)
            .ToArray();

        try
        {
            return await UpsertOnceAsync(incomingRates, cancellationToken);
        }
        catch (DbUpdateException exception)
            when (IsSqlServerUniqueConstraintViolation(exception))
        {
            // Another startup/job instance inserted the same business key
            // after our read. Detach the failed state and re-read exactly once.
            dbContext.ChangeTracker.Clear();
            return await UpsertOnceAsync(incomingRates, cancellationToken);
        }
    }

    private async Task<ExchangeRateUpsertResult> UpsertOnceAsync(
        IReadOnlyCollection<ExchangeRate> incomingRates,
        CancellationToken cancellationToken)
    {
        var firstRateDate = incomingRates.Min(rate => rate.RateDate);
        var lastRateDate = incomingRates.Max(rate => rate.RateDate);
        var baseCurrencyCodes = incomingRates
            .Select(rate => rate.BaseCurrencyCode)
            .Distinct()
            .ToArray();
        var foreignCurrencyCodes = incomingRates
            .Select(rate => rate.ForeignCurrencyCode)
            .Distinct()
            .ToArray();

        var existingRates = await dbContext.ExchangeRates
            .Where(rate =>
                rate.RateDate >= firstRateDate &&
                rate.RateDate <= lastRateDate &&
                baseCurrencyCodes.Contains(rate.BaseCurrencyCode) &&
                foreignCurrencyCodes.Contains(rate.ForeignCurrencyCode))
            .ToListAsync(cancellationToken);

        var existingRatesByKey = existingRates.ToDictionary(CreateKey);
        var insertedCount = 0;
        var updatedCount = 0;
        var unchangedCount = 0;

        foreach (var incomingRate in incomingRates)
        {
            if (existingRatesByKey.TryGetValue(CreateKey(incomingRate), out var existingRate))
            {
                if (existingRate.HasSameRateValues(incomingRate))
                {
                    existingRate.UpdateRates(
                        incomingRate.SourceUpdatedAt,
                        incomingRate.RetrievedAtUtc,
                        incomingRate.ChangeRate,
                        incomingRate.ExchangeRateValue,
                        incomingRate.CashChangeRate,
                        incomingRate.CashExchangeRate,
                        incomingRate.CentralBankChangeRate,
                        incomingRate.CentralBankExchangeRate,
                        incomingRate.CrossRate);

                    unchangedCount++;
                    continue;
                }

                existingRate.UpdateRates(
                    incomingRate.SourceUpdatedAt,
                    incomingRate.RetrievedAtUtc,
                    incomingRate.ChangeRate,
                    incomingRate.ExchangeRateValue,
                    incomingRate.CashChangeRate,
                    incomingRate.CashExchangeRate,
                    incomingRate.CentralBankChangeRate,
                    incomingRate.CentralBankExchangeRate,
                    incomingRate.CrossRate);

                updatedCount++;
                continue;
            }

            dbContext.ExchangeRates.Add(incomingRate);
            insertedCount++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new ExchangeRateUpsertResult(
            insertedCount,
            updatedCount,
            unchangedCount);
    }

    private static bool IsSqlServerUniqueConstraintViolation(Exception exception)
    {
        for (Exception? current = exception;
             current is not null;
             current = current.InnerException)
        {
            if (current is SqlException { Number: 2601 or 2627 })
            {
                return true;
            }
        }

        return false;
    }

    private static ExchangeRateKey CreateKey(ExchangeRate exchangeRate) =>
        new(
            exchangeRate.BaseCurrencyCode,
            exchangeRate.ForeignCurrencyCode,
            exchangeRate.RateDate);

    private readonly record struct ExchangeRateKey(
        int BaseCurrencyCode,
        int ForeignCurrencyCode,
        DateOnly RateDate);
}
