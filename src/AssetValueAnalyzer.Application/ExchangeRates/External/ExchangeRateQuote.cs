namespace AssetValueAnalyzer.Application.ExchangeRates.External;

public sealed record ExchangeRateQuote(
    int BaseCurrencyCode,
    int ForeignCurrencyCode,
    decimal ChangeRate,
    decimal ExchangeRateValue,
    decimal CashChangeRate,
    decimal CashExchangeRate,
    decimal CentralBankChangeRate,
    decimal CentralBankExchangeRate,
    decimal CrossRate,
    DateTime SourceUpdatedAt);
