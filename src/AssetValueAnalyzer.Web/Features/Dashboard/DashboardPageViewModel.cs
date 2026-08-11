using AssetValueAnalyzer.Web.Features.Shared;
using AssetValueAnalyzer.Web.Features.Reports;

namespace AssetValueAnalyzer.Web.Features.Dashboard;

public sealed record DashboardPageViewModel(
    ExchangeRateCardViewModel ExchangeRate,
    DashboardDataFileViewModel? AssetValues,
    DashboardDataFileViewModel? ProducerPriceIndices,
    ReportWizardStateSnapshot WizardState,
    bool HasCompletedReport)
{
    public static DashboardPageViewModel FromSnapshot(
        ReportWorkspaceSnapshot snapshot,
        ExchangeRateCardViewModel exchangeRate) =>
        new(
            exchangeRate,
            DashboardDataFileViewModel.FromSnapshot(snapshot.AssetValues),
            DashboardDataFileViewModel.FromSnapshot(snapshot.ProducerPriceIndices),
            snapshot.CompletedReport is null
                ? snapshot.WizardState
                : ReportWizardStateSnapshot.Empty,
            snapshot.CompletedReport is not null);
}

public sealed record DashboardDataFileViewModel(
    string FileName,
    int ParsedCount,
    DateOnly? FirstMonth,
    DateOnly? LastMonth)
{
    public string? MonthRange =>
        MonthDisplayFormatter.FormatRange(FirstMonth, LastMonth);

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
