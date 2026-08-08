namespace AssetValueAnalyzer.Application.Reports.Calculation;

public sealed record MonthlyFinancialInput(
    DateOnly Month,
    decimal AssetAmount,
    decimal UsdRate,
    decimal ProducerPriceIndex);

public sealed record FinancialImpactReportRow(
    DateOnly Month,
    decimal AssetAmount,
    decimal? MonthlyAssetChangeRate,
    decimal? AssetChangeRate,
    decimal UsdRate,
    decimal DollarizedAmount,
    decimal? MonthlyDollarizedChangeRate,
    decimal? DollarizedChangeRate,
    decimal? DollarizationEffectRate,
    decimal ProducerPriceIndex,
    decimal InflationAdjustedAmount,
    decimal? MonthlyInflationAdjustedChangeRate,
    decimal? InflationAdjustedChangeRate,
    decimal? InflationEffectRate);

public sealed record FinancialImpactReportSummary(
    DateOnly ReportMonth,
    decimal ReportMonthAssetAmount,
    decimal? NominalAssetChangeRate,
    decimal? DollarizedAssetChangeRate,
    decimal? InflationAdjustedAssetChangeRate);

public sealed record FinancialImpactReport(
    FinancialImpactReportSummary Summary,
    IReadOnlyList<FinancialImpactReportRow> Rows);

public sealed record FinancialImpactCalculationError(
    string Code,
    string Message,
    DateOnly? Month = null);

public sealed record FinancialImpactCalculationResult(
    FinancialImpactReport? Report,
    IReadOnlyList<FinancialImpactCalculationError> Errors)
{
    public bool IsValid => Report is not null && Errors.Count == 0;

    public static FinancialImpactCalculationResult Success(
        FinancialImpactReport report) =>
        new(report, []);

    public static FinancialImpactCalculationResult Invalid(
        IReadOnlyList<FinancialImpactCalculationError> errors) =>
        new(null, errors);
}
