using AssetValueAnalyzer.Application.ExchangeRates.Queries;
using Microsoft.EntityFrameworkCore;

namespace AssetValueAnalyzer.Infrastructure.Persistence.ExchangeRates;

public sealed class EfCurrentUsdExchangeRateReader(
    AssetValueAnalyzerDbContext dbContext) : ICurrentUsdExchangeRateReader
{
    private const int UsdCurrencyCode = 1;
    private const int TryCurrencyCode = 56;

    public async Task<CurrentUsdExchangeRate?> ReadAsync(
        CancellationToken cancellationToken = default)
    {
        var latestRates = await dbContext.ExchangeRates
            .AsNoTracking()
            .Where(rate =>
                rate.BaseCurrencyCode == UsdCurrencyCode &&
                rate.ForeignCurrencyCode == TryCurrencyCode)
            .OrderByDescending(rate => rate.RateDate)
            .Take(2)
            .Select(rate => new
            {
                rate.CashChangeRate,
                rate.RateDate,
                rate.RetrievedAtUtc
            })
            .ToArrayAsync(cancellationToken);

        if (latestRates.Length == 0)
        {
            return null;
        }

        var lastCheckedAtUtc = await dbContext.ExchangeRateBackfillCheckpoints
            .AsNoTracking()
            .Select(checkpoint => (DateTimeOffset?)checkpoint.CompletedAtUtc)
            .SingleOrDefaultAsync(cancellationToken)
            ?? latestRates[0].RetrievedAtUtc;

        return new CurrentUsdExchangeRate(
            latestRates[0].CashChangeRate,
            latestRates[0].RateDate,
            lastCheckedAtUtc,
            latestRates.Length > 1
                ? latestRates[1].CashChangeRate
                : null);
    }
}
