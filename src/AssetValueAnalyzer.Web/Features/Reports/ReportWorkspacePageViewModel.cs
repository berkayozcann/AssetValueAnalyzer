using AssetValueAnalyzer.Web.Features.Shared;

namespace AssetValueAnalyzer.Web.Features.Reports;

public sealed record ReportWorkspacePageViewModel(
    ReportWorkspaceStatus Status,
    ExchangeRateCardViewModel ExchangeRate,
    ReportDataFileSummaryViewModel? AssetValues,
    ReportDataFileSummaryViewModel? ProducerPriceIndices)
{
    public bool HasBothFiles => AssetValues is not null && ProducerPriceIndices is not null;

    public int ReadyFileCount =>
        (AssetValues is not null ? 1 : 0) +
        (ProducerPriceIndices is not null ? 1 : 0);

    public int ReadyFileProgressPercent => ReadyFileCount * 50;

    public static ReportWorkspacePageViewModel FromSnapshot(
        ReportWorkspaceSnapshot snapshot,
        ExchangeRateCardViewModel exchangeRate) =>
        new(
            snapshot.Status,
            exchangeRate,
            ReportDataFileSummaryViewModel.FromSnapshot(snapshot.AssetValues),
            ReportDataFileSummaryViewModel.FromSnapshot(snapshot.ProducerPriceIndices));
}

public sealed record ReportDataFileSummaryViewModel(
    string FileName,
    int ParsedCount,
    DateOnly? FirstMonth,
    DateOnly? LastMonth)
{
    public string? MonthRange =>
        MonthDisplayFormatter.FormatRange(FirstMonth, LastMonth);

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
    DraftDataFileKind Kind,
    string Label,
    ReportDataFileSummaryViewModel? File);

public enum DraftDataFileKind
{
    AssetValues,
    ProducerPriceIndices
}
