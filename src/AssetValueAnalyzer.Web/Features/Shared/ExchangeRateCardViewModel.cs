namespace AssetValueAnalyzer.Web.Features.Shared;

public sealed record ExchangeRateCardViewModel(
    string FormattedRate,
    string TrendText,
    string LastSyncText,
    ExchangeRateTrend Trend,
    bool IsDemo = false);

public enum ExchangeRateTrend
{
    Unavailable,
    Unchanged,
    Increased,
    Decreased
}
