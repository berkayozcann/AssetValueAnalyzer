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
    DateOnly AvailableEndMonth);

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
    string InflationEffect);
