namespace AssetValueAnalyzer.Web.Features.Shared;

public sealed record ExchangeRateCardViewModel(
    string FormattedRate,
    string TrendText,
    string LastSyncText,
    ExchangeRateTrend Trend,
    bool IsDemo = false,
    bool HasRate = true)
{
    public string Label { get; init; } = "USD / TRY";

    public string RateDateText { get; init; } = string.Empty;

    public bool IsAwaitingCurrentDayRate { get; init; }
}

public enum ExchangeRateTrend
{
    Unavailable,
    Unchanged,
    Increased,
    Decreased
}
