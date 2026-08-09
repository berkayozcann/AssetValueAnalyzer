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
    ReportKpiTone Tone)
{
    public ReportKpiIcon Icon => Tone switch
    {
        ReportKpiTone.Positive => ReportKpiIcon.Growth,
        ReportKpiTone.Negative => ReportKpiIcon.Decline,
        _ => ReportKpiIcon.Wallet
    };
}

public enum ReportKpiTone
{
    Brand,
    Positive,
    Negative
}

public enum ReportKpiIcon
{
    Wallet,
    Growth,
    Decline
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
