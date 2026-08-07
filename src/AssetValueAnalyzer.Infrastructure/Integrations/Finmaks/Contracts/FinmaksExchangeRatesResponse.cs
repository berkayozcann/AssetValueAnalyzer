using System.Text.Json.Serialization;

namespace AssetValueAnalyzer.Infrastructure.Integrations.Finmaks.Contracts;

internal sealed class FinmaksExchangeRatesResponse
{
    public IReadOnlyList<FinmaksExchangeRateItem> ExchangeRates { get; init; } = [];

    public FinmaksResponseHeader Header { get; init; } = new();
}

internal sealed class FinmaksExchangeRateItem
{
    public int BaseCurrencyCode { get; init; }

    public int ForeignCurrencyCode { get; init; }

    public decimal ChangeRate { get; init; }

    [JsonPropertyName("ExchangeRate")]
    public decimal ExchangeRateValue { get; init; }

    public decimal CashChangeRate { get; init; }

    public decimal CashExchangeRate { get; init; }

    public decimal CentralBankChangeRate { get; init; }

    public decimal CentralBankExchangeRate { get; init; }

    public decimal CrossRate { get; init; }

    [JsonPropertyName("CurrentDate")]
    public DateTime SourceUpdatedAt { get; init; }
}

internal sealed class FinmaksResponseHeader
{
    public int Status { get; init; }

    public string ResponseCode { get; init; } = string.Empty;

    public string ResponseMessage { get; init; } = string.Empty;
}
