using AssetValueAnalyzer.Application.ExchangeRates.Queries;
using AssetValueAnalyzer.Domain.ExchangeRates;
using Microsoft.EntityFrameworkCore;

namespace AssetValueAnalyzer.Infrastructure.Persistence.ExchangeRates;

public sealed class EfExchangeRateReader(
    AssetValueAnalyzerDbContext dbContext) : IExchangeRateReader
{
    public async Task<IReadOnlyList<ExchangeRateReadModel>> ReadLatestAsync(
        LatestExchangeRateQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var filteredRates = ApplyCurrencyFilters(
            dbContext.ExchangeRates.AsNoTracking(),
            query.BaseCurrencyCode,
            query.ForeignCurrencyCode);

        var rateDate = query.RateDate ?? await filteredRates
            .Select(rate => (DateOnly?)rate.RateDate)
            .MaxAsync(cancellationToken);

        if (rateDate is null)
        {
            return [];
        }

        return await Project(filteredRates
                .Where(rate => rate.RateDate == rateDate)
                .OrderBy(rate => rate.BaseCurrencyCode)
                .ThenBy(rate => rate.ForeignCurrencyCode)
                .Take(query.Limit))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ExchangeRateReadModel>> ReadRangeAsync(
        ExchangeRateRangeQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var filteredRates = ApplyCurrencyFilters(
            dbContext.ExchangeRates.AsNoTracking(),
            query.BaseCurrencyCode,
            query.ForeignCurrencyCode);

        return await Project(filteredRates
                .Where(rate =>
                    rate.RateDate >= query.StartDate &&
                    rate.RateDate <= query.EndDate)
                .OrderByDescending(rate => rate.RateDate)
                .ThenBy(rate => rate.BaseCurrencyCode)
                .ThenBy(rate => rate.ForeignCurrencyCode)
                .Take(query.Limit))
            .ToArrayAsync(cancellationToken);
    }

    private static IQueryable<ExchangeRate> ApplyCurrencyFilters(
        IQueryable<ExchangeRate> rates,
        int? baseCurrencyCode,
        int? foreignCurrencyCode) =>
        rates.Where(rate =>
            (!baseCurrencyCode.HasValue ||
                rate.BaseCurrencyCode == baseCurrencyCode) &&
            (!foreignCurrencyCode.HasValue ||
                rate.ForeignCurrencyCode == foreignCurrencyCode));

    private static IQueryable<ExchangeRateReadModel> Project(
        IQueryable<ExchangeRate> rates) =>
        rates.Select(rate => new ExchangeRateReadModel(
            rate.BaseCurrencyCode,
            rate.ForeignCurrencyCode,
            rate.RateDate,
            rate.ChangeRate,
            rate.ExchangeRateValue,
            rate.CashChangeRate,
            rate.CashExchangeRate,
            rate.CentralBankChangeRate,
            rate.CentralBankExchangeRate,
            rate.CrossRate,
            rate.SourceUpdatedAt,
            rate.RetrievedAtUtc));
}
