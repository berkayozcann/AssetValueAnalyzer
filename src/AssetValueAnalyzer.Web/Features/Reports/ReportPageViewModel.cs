using AssetValueAnalyzer.Application.Reports.Calculation;
using AssetValueAnalyzer.Web.Features.Shared;

namespace AssetValueAnalyzer.Web.Features.Reports;

public sealed record ReportPageViewModel(
    string Period,
    ExchangeRateCardViewModel ExchangeRate,
    IReadOnlyList<ReportKpiViewModel> Kpis,
    IReadOnlyList<ReportRowViewModel> Rows,
    DateOnly StartMonth,
    DateOnly EndMonth,
    DateOnly AvailableStartMonth,
    DateOnly AvailableEndMonth,
    FinancialImpactReport? ExportData = null)
{
    public ReportSortColumn SortColumn { get; init; } = ReportSortColumn.Month;

    public ReportSortDirection SortDirection { get; init; } = ReportSortDirection.Ascending;
}

public sealed record ReportKpiViewModel(
    string Label,
    string Value,
    string Description,
    ReportKpiTone Tone,
    ReportKpiIcon Icon);

public enum ReportKpiTone
{
    Brand,
    Positive,
    Negative
}

public enum ReportKpiIcon
{
    AssetAmount,
    NominalChange,
    DollarizedChange,
    InflationAdjustedChange
}

public sealed record ReportRowViewModel(
    string Month,
    string AssetValue,
    string MonthlyAssetIncreaseRate,
    string AssetChangeRate,
    string UsdRate,
    string DollarizedAmount,
    string MonthlyDollarizedIncreaseRate,
    string DollarizedChangeRate,
    string DollarizationEffect,
    string ProducerPriceIndex,
    string InflationAdjustedAmount,
    string MonthlyInflationAdjustedIncreaseRate,
    string InflationAdjustedChangeRate,
    string InflationEffect,
    ReportRowSortValues SortValues);

public sealed record ReportRowSortValues(
    DateOnly Month,
    decimal AssetValue,
    decimal? MonthlyAssetIncreaseRate,
    decimal? AssetChangeRate,
    decimal UsdRate,
    decimal DollarizedAmount,
    decimal? MonthlyDollarizedIncreaseRate,
    decimal? DollarizedChangeRate,
    decimal? DollarizationEffect,
    decimal ProducerPriceIndex,
    decimal InflationAdjustedAmount,
    decimal? MonthlyInflationAdjustedIncreaseRate,
    decimal? InflationAdjustedChangeRate,
    decimal? InflationEffect);

public sealed record ReportSortLinkViewModel(
    string Label,
    string Url,
    string SortKey,
    bool IsActive,
    ReportSortDirection Direction);
