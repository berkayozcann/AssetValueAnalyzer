using AssetValueAnalyzer.Application.Assets.Imports;
using AssetValueAnalyzer.Application.ProducerPriceIndices.Imports;
using AssetValueAnalyzer.Web.Features.Reports;

namespace AssetValueAnalyzer.IntegrationTests.Support;

internal sealed class TestReportWorkspaceSession : IReportWorkspaceSession
{
    private ReportWorkspaceSnapshot snapshot = ReportWorkspaceSnapshot.Empty;

    public ReportWorkspaceSnapshot Get() => snapshot;

    public void SaveAssetValues(
        string fileName,
        IReadOnlyList<MonthlyAssetValueInput> values)
    {
        snapshot = snapshot with
        {
            AssetValues = new ReportDataFileSnapshot(
                fileName,
                values
                    .Select(value => new ReportMonthlyValueSnapshot(value.Month, value.Amount))
                    .ToArray()),
            CompletedReport = null
        };
    }

    public void SaveProducerPriceIndices(
        string fileName,
        IReadOnlyList<MonthlyProducerPriceIndexInput> values)
    {
        snapshot = snapshot with
        {
            ProducerPriceIndices = new ReportDataFileSnapshot(
                fileName,
                values
                    .Select(value => new ReportMonthlyValueSnapshot(value.Month, value.Value))
                    .ToArray()),
            CompletedReport = null
        };
    }

    public void ClearAssetValues() =>
        snapshot = snapshot with
        {
            AssetValues = null,
            CompletedReport = null
        };

    public void ClearProducerPriceIndices() =>
        snapshot = snapshot with
        {
            ProducerPriceIndices = null,
            CompletedReport = null
        };

    public void SaveCompletedReport(ReportPageViewModel report) =>
        snapshot = snapshot with { CompletedReport = report };

    public void Clear() => snapshot = ReportWorkspaceSnapshot.Empty;
}
