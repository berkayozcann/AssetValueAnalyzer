using AssetValueAnalyzer.Web.Features.Shared;
using AssetValueAnalyzer.Web.Features.Reports;
using AssetValueAnalyzer.Application.Reports.Creation;

namespace AssetValueAnalyzer.Web.Features.Dashboard;

public sealed record DashboardPageViewModel(
    ExchangeRateCardViewModel ExchangeRate,
    DashboardDataFileViewModel? AssetValues,
    DashboardDataFileViewModel? ProducerPriceIndices,
    ReportWizardStateSnapshot WizardState,
    bool HasCompletedReport,
    DashboardRangeValidationViewModel RangeValidation)
{
    public static DashboardPageViewModel FromSnapshot(
        ReportWorkspaceSnapshot snapshot,
        ExchangeRateCardViewModel exchangeRate,
        DashboardRangeValidationViewModel? rangeValidation = null) =>
        new(
            exchangeRate,
            DashboardDataFileViewModel.FromSnapshot(snapshot.AssetValues),
            DashboardDataFileViewModel.FromSnapshot(snapshot.ProducerPriceIndices),
            snapshot.CompletedReport is null
                ? snapshot.WizardState
                : ReportWizardStateSnapshot.Empty,
            snapshot.CompletedReport is not null,
            rangeValidation ?? DashboardRangeValidationViewModel.Idle);
}

public sealed record DashboardRangeValidationViewModel(
    string State,
    string? ErrorMessage,
    int? IncludedMonthCount)
{
    public static DashboardRangeValidationViewModel Idle { get; } =
        new("idle", null, null);

    public static DashboardRangeValidationViewModel FromResult(
        FinancialImpactReportRangeValidationResult result) =>
        result.IsValid
            ? new("valid", null, result.SelectedAssetValues.Count)
            : new("invalid", result.Errors.FirstOrDefault()?.Message, null);
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
