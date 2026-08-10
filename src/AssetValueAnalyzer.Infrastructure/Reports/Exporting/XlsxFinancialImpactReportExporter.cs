using System.Globalization;
using AssetValueAnalyzer.Application.Reports.Calculation;
using AssetValueAnalyzer.Application.Reports.Exporting;
using ClosedXML.Excel;

namespace AssetValueAnalyzer.Infrastructure.Reports.Exporting;

public sealed class XlsxFinancialImpactReportExporter
    : IFinancialImpactReportExporter
{
    public const string XlsxContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private const int HeaderRowNumber = 4;
    private const int ColumnCount = 14;
    private const string CurrencyNumberFormat =
        "\"₺\"#,##0.00;[Red]-\"₺\"#,##0.00;\"₺\"0.00";
    private const string PercentageNumberFormat =
        "+0.00%;[Red]-0.00%;0.00%";
    private static readonly CultureInfo TurkishCulture =
        CultureInfo.GetCultureInfo("tr-TR");

    private static readonly string[] Headers =
    [
        "Tarih",
        "Varlık Tutarı",
        "Önceki Aya Göre Varlık Artışı",
        "Varlık Değişim Oranı",
        "USD Kuru",
        "Dolarizasyon Varlık Tutarı",
        "Dolarizasyon Önceki Aya Göre Varlık Artışı",
        "Dolarizasyon Varlık Değişim Oranı",
        "Dolarizasyon Etkisi (%)",
        "Yİ-ÜFE Endeksi",
        "Enflasyon Varlık Tutarı",
        "Enflasyon Önceki Aya Göre Varlık Artışı",
        "Enflasyon Varlık Değişim Oranı",
        "Enflasyon Etkisi (%)"
    ];

    public FinancialImpactReportExport Export(FinancialImpactReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        if (report.Rows.Count == 0)
        {
            throw new ArgumentException(
                "Excel raporu oluşturmak için en az bir finansal detay satırı gereklidir.",
                nameof(report));
        }

        using var workbook = CreateWorkbook(report);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        return new FinancialImpactReportExport(
            stream.ToArray(),
            XlsxContentType,
            $"finansal-etki-raporu-{report.Rows[0].Month:yyyy-MM}-{report.Rows[^1].Month:yyyy-MM}.xlsx");
    }

    private static XLWorkbook CreateWorkbook(FinancialImpactReport report)
    {
        var workbook = new XLWorkbook();
        workbook.Properties.Title = "Finansal Etki Raporu";
        workbook.Properties.Subject = "Aylık finansal etki analizi";
        workbook.Properties.Author = "AssetValueAnalyzer";

        var worksheet = workbook.Worksheets.Add("Finansal Etki Raporu");
        worksheet.ShowGridLines = false;

        AddTitle(worksheet, report);
        AddHeaders(worksheet);
        AddRows(worksheet, report.Rows);
        ApplyLayout(worksheet, report.Rows.Count);

        return workbook;
    }

    private static void AddTitle(
        IXLWorksheet worksheet,
        FinancialImpactReport report)
    {
        var titleRange = worksheet.Range(1, 1, 1, ColumnCount);
        titleRange.Merge();
        titleRange.Value = "Finansal Etki Raporu";
        titleRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#0B1715");
        titleRange.Style.Font.FontColor = XLColor.FromHtml("#F7F4EC");
        titleRange.Style.Font.Bold = true;
        titleRange.Style.Font.FontSize = 18;
        titleRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        titleRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        worksheet.Row(1).Height = 30;

        var periodRange = worksheet.Range(2, 1, 2, ColumnCount);
        periodRange.Merge();
        periodRange.Value =
            $"Rapor Dönemi: {FormatMonth(report.Rows[0].Month)} – {FormatMonth(report.Rows[^1].Month)}";
        periodRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#E7E5DE");
        periodRange.Style.Font.FontColor = XLColor.FromHtml("#245C63");
        periodRange.Style.Font.Bold = true;
        periodRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        worksheet.Row(2).Height = 24;
    }

    private static void AddHeaders(IXLWorksheet worksheet)
    {
        for (var index = 0; index < Headers.Length; index++)
        {
            worksheet.Cell(HeaderRowNumber, index + 1).Value = Headers[index];
        }

        var headerRange = worksheet.Range(
            HeaderRowNumber,
            1,
            HeaderRowNumber,
            ColumnCount);
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#245C63");
        headerRange.Style.Font.FontColor = XLColor.White;
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        headerRange.Style.Alignment.WrapText = true;
        headerRange.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        headerRange.Style.Border.BottomBorderColor = XLColor.FromHtml("#A8C5C2");
        worksheet.Row(HeaderRowNumber).Height = 48;
    }

    private static void AddRows(
        IXLWorksheet worksheet,
        IReadOnlyList<FinancialImpactReportRow> rows)
    {
        for (var index = 0; index < rows.Count; index++)
        {
            var rowNumber = HeaderRowNumber + index + 1;
            var row = rows[index];

            worksheet.Cell(rowNumber, 1).Value = row.Month.ToDateTime(TimeOnly.MinValue);
            worksheet.Cell(rowNumber, 2).Value = row.AssetAmount;

            SetMonthlyChangeRate(
                worksheet.Cell(rowNumber, 3),
                row.MonthlyAssetChangeRate);
            SetMonthlyChangeRate(
                worksheet.Cell(rowNumber, 7),
                row.MonthlyDollarizedChangeRate);
            SetMonthlyChangeRate(
                worksheet.Cell(rowNumber, 12),
                row.MonthlyInflationAdjustedChangeRate);

            SetNullableDecimal(worksheet.Cell(rowNumber, 4), row.AssetChangeRate);
            worksheet.Cell(rowNumber, 5).Value = row.UsdRate;
            worksheet.Cell(rowNumber, 6).Value = row.DollarizedAmount;
            SetNullableDecimal(worksheet.Cell(rowNumber, 8), row.DollarizedChangeRate);
            SetNullableDecimal(worksheet.Cell(rowNumber, 9), row.DollarizationEffectRate);
            worksheet.Cell(rowNumber, 10).Value = row.ProducerPriceIndex;
            worksheet.Cell(rowNumber, 11).Value = row.InflationAdjustedAmount;
            SetNullableDecimal(worksheet.Cell(rowNumber, 13), row.InflationAdjustedChangeRate);
            SetNullableDecimal(worksheet.Cell(rowNumber, 14), row.InflationEffectRate);

            if (index % 2 == 1)
            {
                worksheet.Range(rowNumber, 1, rowNumber, ColumnCount)
                    .Style.Fill.BackgroundColor = XLColor.FromHtml("#F4F2EC");
            }
        }
    }

    private static void ApplyLayout(
        IXLWorksheet worksheet,
        int rowCount)
    {
        var firstDataRow = HeaderRowNumber + 1;
        var lastDataRow = HeaderRowNumber + rowCount;
        var dataRange = worksheet.Range(firstDataRow, 1, lastDataRow, ColumnCount);

        worksheet.Range(1, 1, lastDataRow, ColumnCount)
            .Style.Font.FontName = "Arial";
        dataRange.Style.Font.FontColor = XLColor.FromHtml("#18201E");
        dataRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        dataRange.Style.Border.BottomBorder = XLBorderStyleValues.Hair;
        dataRange.Style.Border.BottomBorderColor = XLColor.FromHtml("#D7D3C8");

        worksheet.Range(firstDataRow, 1, lastDataRow, 1)
            .Style.NumberFormat.Format = "[$-041F]mmmm yyyy";

        foreach (var columnNumber in new[] { 2, 6, 11 })
        {
            worksheet.Range(firstDataRow, columnNumber, lastDataRow, columnNumber)
                .Style.NumberFormat.Format = CurrencyNumberFormat;
        }

        foreach (var columnNumber in new[] { 3, 4, 7, 8, 9, 12, 13, 14 })
        {
            worksheet.Range(firstDataRow, columnNumber, lastDataRow, columnNumber)
                .Style.NumberFormat.Format = PercentageNumberFormat;
        }

        worksheet.Range(firstDataRow, 5, lastDataRow, 5)
            .Style.NumberFormat.Format = "0.0000";
        worksheet.Range(firstDataRow, 10, lastDataRow, 10)
            .Style.NumberFormat.Format = "0.00";

        worksheet.Range(firstDataRow, 2, lastDataRow, ColumnCount)
            .Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        worksheet.Range(firstDataRow, 1, lastDataRow, 1)
            .Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

        worksheet.Range(HeaderRowNumber, 1, lastDataRow, ColumnCount)
            .SetAutoFilter();
        worksheet.SheetView.FreezeRows(HeaderRowNumber);
        worksheet.SheetView.FreezeColumns(1);

        double[] widths = [16, 18, 24, 22, 13, 23, 30, 28, 22, 17, 23, 30, 28, 21];

        for (var columnNumber = 1; columnNumber <= widths.Length; columnNumber++)
        {
            worksheet.Column(columnNumber).Width = widths[columnNumber - 1];
        }

        worksheet.Rows(firstDataRow, lastDataRow).Height = 22;
    }

    private static void SetNullableDecimal(IXLCell cell, decimal? value)
    {
        if (value.HasValue)
        {
            cell.Value = value.Value;
        }
    }

    private static void SetMonthlyChangeRate(IXLCell cell, decimal? value)
    {
        cell.Value = value ?? 0m;
    }

    private static string FormatMonth(DateOnly month)
    {
        var formatted = month.ToDateTime(TimeOnly.MinValue)
            .ToString("MMMM yyyy", TurkishCulture);

        return TurkishCulture.TextInfo.ToTitleCase(formatted);
    }
}
