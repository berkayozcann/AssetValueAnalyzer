using AssetValueAnalyzer.Application.ExchangeRates.Queries;

namespace AssetValueAnalyzer.Api.Features.ExchangeRates;

public sealed record ExchangeRateResponse(
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
    DateTimeOffset RetrievedAtUtc)
{
    public static ExchangeRateResponse FromReadModel(ExchangeRateReadModel rate) =>
        new(
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
            rate.RetrievedAtUtc);
}
