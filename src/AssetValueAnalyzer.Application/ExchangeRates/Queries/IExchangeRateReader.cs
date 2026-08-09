namespace AssetValueAnalyzer.Application.ExchangeRates.Queries;

public interface IExchangeRateReader
{
    Task<IReadOnlyList<ExchangeRateReadModel>> ReadLatestAsync(
        LatestExchangeRateQuery query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExchangeRateReadModel>> ReadRangeAsync(
        ExchangeRateRangeQuery query,
        CancellationToken cancellationToken = default);
}

public sealed record LatestExchangeRateQuery(
    DateOnly? RateDate,
    int? BaseCurrencyCode,
    int? ForeignCurrencyCode,
    int Limit);

public sealed record ExchangeRateRangeQuery(
    DateOnly StartDate,
    DateOnly EndDate,
    int? BaseCurrencyCode,
    int? ForeignCurrencyCode,
    int Limit);

public sealed record ExchangeRateReadModel(
    int BaseCurrencyCode,
    int ForeignCurrencyCode,
    DateOnly RateDate,
    decimal ChangeRate,
    decimal ExchangeRateValue,
    decimal CashChangeRate,
    decimal CashExchangeRate,
    decimal CentralBankChangeRate,
    decimal CentralBankExchangeRate,
    decimal CrossRate,
    DateTime SourceUpdatedAt,
    DateTimeOffset RetrievedAtUtc);
