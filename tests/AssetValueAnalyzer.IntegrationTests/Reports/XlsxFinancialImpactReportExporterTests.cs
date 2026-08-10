using AssetValueAnalyzer.Application.Reports.Calculation;
using AssetValueAnalyzer.Infrastructure.Reports.Exporting;
using ClosedXML.Excel;

namespace AssetValueAnalyzer.IntegrationTests.Reports;

public sealed class XlsxFinancialImpactReportExporterTests
{
    [Fact]
    public void Export_WithFinancialRows_CreatesTypedFourteenColumnWorkbook()
    {
        var exporter = new XlsxFinancialImpactReportExporter();
        var report = CreateReport();

        var export = exporter.Export(report);

        Assert.Equal(XlsxFinancialImpactReportExporter.XlsxContentType, export.ContentType);
        Assert.Equal("finansal-etki-raporu-2021-12-2022-01.xlsx", export.FileName);
        Assert.NotEmpty(export.Content);

        using var stream = new MemoryStream(export.Content);
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheet("Finansal Etki Raporu");

        Assert.Equal("Finansal Etki Raporu", worksheet.Cell(1, 1).GetString());
        Assert.Contains("Aralık 2021 – Ocak 2022", worksheet.Cell(2, 1).GetString());
        Assert.Equal(14, worksheet.Row(4).CellsUsed().Count());
        Assert.Equal("Tarih", worksheet.Cell(4, 1).GetString());
        Assert.Equal("Enflasyon Etkisi (%)", worksheet.Cell(4, 14).GetString());

        Assert.Equal(XLDataType.DateTime, worksheet.Cell(5, 1).DataType);
        Assert.Equal(new DateTime(2021, 12, 1), worksheet.Cell(5, 1).GetDateTime());
        Assert.Equal(XLDataType.Number, worksheet.Cell(5, 2).DataType);
        Assert.Equal(1_000m, worksheet.Cell(5, 2).GetValue<decimal>());
        Assert.Equal(XLDataType.Number, worksheet.Cell(5, 3).DataType);
        Assert.Equal(0m, worksheet.Cell(5, 3).GetValue<decimal>());
        Assert.Equal(0m, worksheet.Cell(5, 7).GetValue<decimal>());
        Assert.Equal(0m, worksheet.Cell(5, 12).GetValue<decimal>());
        Assert.Contains("%", worksheet.Cell(5, 3).Style.NumberFormat.Format);
        Assert.Equal(10m, worksheet.Cell(5, 5).GetValue<decimal>());
        Assert.Equal(100m, worksheet.Cell(5, 10).GetValue<decimal>());

        Assert.Equal(0.10m, worksheet.Cell(6, 3).GetValue<decimal>());
        Assert.Equal(1_100m, worksheet.Cell(6, 6).GetValue<decimal>());
        Assert.Equal(0.25m, worksheet.Cell(6, 14).GetValue<decimal>());
        Assert.Contains("₺", worksheet.Cell(5, 2).Style.NumberFormat.Format);
        Assert.Contains("%", worksheet.Cell(6, 3).Style.NumberFormat.Format);
        Assert.Equal("0.0000", worksheet.Cell(5, 5).Style.NumberFormat.Format);
        Assert.Equal("0.00", worksheet.Cell(5, 10).Style.NumberFormat.Format);
        Assert.True(worksheet.AutoFilter.IsEnabled);
    }

    [Fact]
    public void Export_WhenPreviousCalendarMonthIsMissing_WritesNumericZeroForMonthlyChanges()
    {
        var exporter = new XlsxFinancialImpactReportExporter();
        var report = CreateReport();
        var marchRow = report.Rows[1] with
        {
            Month = new DateOnly(2022, 3, 1),
            MonthlyAssetChangeRate = null,
            MonthlyDollarizedChangeRate = null,
            MonthlyInflationAdjustedChangeRate = null
        };
        var reportWithGap = report with { Rows = [report.Rows[0], marchRow] };

        var export = exporter.Export(reportWithGap);

        using var stream = new MemoryStream(export.Content);
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheet("Finansal Etki Raporu");

        Assert.Equal(new DateTime(2022, 3, 1), worksheet.Cell(6, 1).GetDateTime());
        Assert.Equal(XLDataType.Number, worksheet.Cell(6, 3).DataType);
        Assert.Equal(0m, worksheet.Cell(6, 3).GetValue<decimal>());
        Assert.Equal(0m, worksheet.Cell(6, 7).GetValue<decimal>());
        Assert.Equal(0m, worksheet.Cell(6, 12).GetValue<decimal>());
        Assert.Contains("%", worksheet.Cell(6, 3).Style.NumberFormat.Format);
    }

    [Fact]
    public void Export_WithoutRows_RejectsInvalidReport()
    {
        var exporter = new XlsxFinancialImpactReportExporter();
        var report = new FinancialImpactReport(
            new FinancialImpactReportSummary(
                new DateOnly(2022, 1, 1),
                0m,
                null,
                null,
                null),
            []);

        var exception = Assert.Throws<ArgumentException>(() => exporter.Export(report));

        Assert.Contains("en az bir finansal detay satırı", exception.Message);
    }

    private static FinancialImpactReport CreateReport() =>
        new(
            new FinancialImpactReportSummary(
                new DateOnly(2022, 1, 1),
                1_100m,
                0.10m,
                0.05m,
                0.02m),
            [
                new FinancialImpactReportRow(
                    new DateOnly(2021, 12, 1),
                    1_000m,
                    null,
                    0.10m,
                    10m,
                    1_200m,
                    null,
                    0.20m,
                    0.20m,
                    100m,
                    1_250m,
                    null,
                    0.25m,
                    0.25m),
                new FinancialImpactReportRow(
                    new DateOnly(2022, 1, 1),
                    1_100m,
                    0.10m,
                    0m,
                    20m,
                    1_100m,
                    -0.0833333333m,
                    0m,
                    0m,
                    125m,
                    1_100m,
                    -0.12m,
                    0m,
                    0.25m)
            ]);
}
