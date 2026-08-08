namespace AssetValueAnalyzer.Web.Features.Shared;

public sealed record ExchangeRateCardViewModel(
    string FormattedRate,
    string TrendText,
    string LastSyncText,
    ExchangeRateTrend Trend,
    bool IsDemo = false)
{
    public string Label { get; init; } = "Güncel USD/TRY";
}

public enum ExchangeRateTrend
{
    Unavailable,
    Unchanged,
    Increased,
    Decreased
}
