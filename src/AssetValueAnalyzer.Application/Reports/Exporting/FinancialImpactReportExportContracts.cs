using AssetValueAnalyzer.Application.Reports.Calculation;

namespace AssetValueAnalyzer.Application.Reports.Exporting;

public interface IFinancialImpactReportExporter
{
    FinancialImpactReportExport Export(FinancialImpactReport report);
}

public sealed record FinancialImpactReportExport(
    byte[] Content,
    string ContentType,
    string FileName);
