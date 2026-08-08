using AssetValueAnalyzer.Web.Features.Shared;
using AssetValueAnalyzer.Web.Features.Reports;

namespace AssetValueAnalyzer.Web.Features.Dashboard;

public sealed record DashboardPageViewModel(
    ExchangeRateCardViewModel ExchangeRate,
    DashboardDataFileViewModel? AssetValues,
    DashboardDataFileViewModel? ProducerPriceIndices)
{
    public static DashboardPageViewModel FromSnapshot(
        ReportWorkspaceSnapshot snapshot) =>
        new(
            new ExchangeRateCardViewModel(
                FormattedRate: "41,2874",
                TrendText: "Artış",
                LastSyncText: "Tasarım önizlemesi",
                Trend: ExchangeRateTrend.Increased,
                IsDemo: true),
            DashboardDataFileViewModel.FromSnapshot(snapshot.AssetValues),
            DashboardDataFileViewModel.FromSnapshot(snapshot.ProducerPriceIndices));
}

public sealed record DashboardDataFileViewModel(
    string FileName,
    int ParsedCount,
    DateOnly? FirstMonth,
    DateOnly? LastMonth)
{
    public static DashboardDataFileViewModel? FromSnapshot(
        ReportDataFileSnapshot? snapshot) =>
        snapshot is null
            ? null
            : new(
                snapshot.FileName,
                snapshot.ParsedCount,
                snapshot.FirstMonth,
                snapshot.LastMonth);
}
