using AssetValueAnalyzer.Web.Features.Shared;

namespace AssetValueAnalyzer.Web.Features.Reports;

public sealed record ReportWorkspacePageViewModel(
    ReportWorkspaceStatus Status,
    ExchangeRateCardViewModel ExchangeRate,
    ReportDataFileSummaryViewModel? AssetValues,
    ReportDataFileSummaryViewModel? ProducerPriceIndices)
{
    public bool HasBothFiles => AssetValues is not null && ProducerPriceIndices is not null;

    public static ReportWorkspacePageViewModel FromSnapshot(
        ReportWorkspaceSnapshot snapshot) =>
        new(
            snapshot.Status,
            CreateExchangeRatePreview(),
            ReportDataFileSummaryViewModel.FromSnapshot(snapshot.AssetValues),
            ReportDataFileSummaryViewModel.FromSnapshot(snapshot.ProducerPriceIndices));

    private static ExchangeRateCardViewModel CreateExchangeRatePreview() =>
        new(
            FormattedRate: "41,2874",
            TrendText: "Artış",
            LastSyncText: "Tasarım önizlemesi",
            Trend: ExchangeRateTrend.Increased,
            IsDemo: true);
}

public sealed record ReportDataFileSummaryViewModel(
    string FileName,
    int ParsedCount,
    DateOnly? FirstMonth,
    DateOnly? LastMonth)
{
    public static ReportDataFileSummaryViewModel? FromSnapshot(
        ReportDataFileSnapshot? snapshot) =>
        snapshot is null
            ? null
            : new(
                snapshot.FileName,
                snapshot.ParsedCount,
                snapshot.FirstMonth,
                snapshot.LastMonth);
}

public sealed record DraftFileStatusViewModel(
    string Label,
    ReportDataFileSummaryViewModel? File);
