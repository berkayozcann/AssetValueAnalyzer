using AssetValueAnalyzer.Application.Reports.Creation;
using Microsoft.EntityFrameworkCore;

namespace AssetValueAnalyzer.Infrastructure.Persistence.ExchangeRates;

public sealed class EfUsdCashChangeRateReader(
    AssetValueAnalyzerDbContext dbContext) : IUsdCashChangeRateReader
{
    private const int UsdCurrencyCode = 1;
    private const int TryCurrencyCode = 56;

    public async Task<IReadOnlyList<UsdCashChangeRate>> ReadAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        if (startDate > endDate)
        {
            throw new ArgumentException(
                "The exchange-rate start date cannot be after the end date.",
                nameof(startDate));
        }

        return await dbContext.ExchangeRates
            .AsNoTracking()
            .Where(rate =>
                rate.BaseCurrencyCode == UsdCurrencyCode &&
                rate.ForeignCurrencyCode == TryCurrencyCode &&
                rate.RateDate >= startDate &&
                rate.RateDate <= endDate)
            .OrderBy(rate => rate.RateDate)
            .Select(rate => new UsdCashChangeRate(
                rate.RateDate,
                rate.CashChangeRate))
            .ToListAsync(cancellationToken);
    }
}
