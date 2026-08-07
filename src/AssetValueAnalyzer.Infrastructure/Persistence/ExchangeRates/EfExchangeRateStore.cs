using AssetValueAnalyzer.Application.ExchangeRates.Synchronization;
using AssetValueAnalyzer.Domain.ExchangeRates;
using Microsoft.EntityFrameworkCore;

namespace AssetValueAnalyzer.Infrastructure.Persistence.ExchangeRates;

public sealed class EfExchangeRateStore(
    AssetValueAnalyzerDbContext dbContext) : IExchangeRateStore
{
    public Task<DateOnly?> GetLatestRateDateAsync(
        CancellationToken cancellationToken = default) =>
        dbContext.ExchangeRates
            .AsNoTracking()
            .OrderByDescending(rate => rate.RateDate)
            .Select(rate => (DateOnly?)rate.RateDate)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<ExchangeRateUpsertResult> UpsertAsync(
        IReadOnlyCollection<ExchangeRate> exchangeRates,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(exchangeRates);

        if (exchangeRates.Count == 0)
        {
            return new ExchangeRateUpsertResult(0, 0);
        }

        var incomingRates = exchangeRates
            .GroupBy(CreateKey)
            .Select(group => group.MaxBy(rate => rate.SourceUpdatedAt)!)
            .ToArray();

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

        foreach (var incomingRate in incomingRates)
        {
            if (existingRatesByKey.TryGetValue(CreateKey(incomingRate), out var existingRate))
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

                updatedCount++;
                continue;
            }

            dbContext.ExchangeRates.Add(incomingRate);
            insertedCount++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new ExchangeRateUpsertResult(insertedCount, updatedCount);
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
