using AssetValueAnalyzer.Web.Features.Shared;

namespace AssetValueAnalyzer.Web.Features.Dashboard;

public sealed record DashboardPageViewModel(ExchangeRateCardViewModel ExchangeRate)
{
    public static DashboardPageViewModel CreateDesignPreview() =>
        new(new ExchangeRateCardViewModel(
            FormattedRate: "41,2874",
            TrendText: "Artış",
            LastSyncText: "Tasarım önizlemesi",
            Trend: ExchangeRateTrend.Increased,
            IsDemo: true));
}
