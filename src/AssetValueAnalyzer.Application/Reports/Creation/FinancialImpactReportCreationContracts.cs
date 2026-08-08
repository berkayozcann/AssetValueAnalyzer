using AssetValueAnalyzer.Application.Assets.Imports;
using AssetValueAnalyzer.Application.ProducerPriceIndices.Imports;
using AssetValueAnalyzer.Application.Reports.Calculation;

namespace AssetValueAnalyzer.Application.Reports.Creation;

public sealed record CreateFinancialImpactReportRequest(
    IReadOnlyList<MonthlyAssetValueInput> AssetValues,
    IReadOnlyList<MonthlyProducerPriceIndexInput> ProducerPriceIndices,
    DateOnly? StartMonth = null,
    DateOnly? EndMonth = null);

public sealed record FinancialImpactReportCreationError(
    string Code,
    string Message,
    DateOnly? Month = null);

public sealed record FinancialImpactReportCreationResult(
    FinancialImpactReport? Report,
    IReadOnlyList<FinancialImpactReportCreationError> Errors)
{
    public bool IsValid => Report is not null && Errors.Count == 0;

    public static FinancialImpactReportCreationResult Success(
        FinancialImpactReport report) =>
        new(report, []);

    public static FinancialImpactReportCreationResult Invalid(
        IReadOnlyList<FinancialImpactReportCreationError> errors) =>
        new(null, errors);
}
