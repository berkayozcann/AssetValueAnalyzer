using System.Globalization;
using AssetValueAnalyzer.Application.Reports.Calculation;
using AssetValueAnalyzer.Web.Features.Shared;

namespace AssetValueAnalyzer.Web.Features.Reports;

public static class ReportPageViewModelFactory
{
    private static readonly CultureInfo TurkishCulture = CultureInfo.GetCultureInfo("tr-TR");

    public static ReportPageViewModel Create(
        FinancialImpactReport report,
        ExchangeRateCardViewModel exchangeRate)
    {
        ArgumentNullException.ThrowIfNull(report);

        var firstRow = report.Rows[0];
        var lastRow = report.Rows[^1];

        return new ReportPageViewModel(
            Period: $"{FormatMonth(firstRow.Month)} – {FormatMonth(lastRow.Month)}",
            ExchangeRate: exchangeRate,
            Kpis:
            [
                new(
                    "Rapor Ayı Varlık Tutarı",
                    FormatMoney(report.Summary.ReportMonthAssetAmount),
                    $"{FormatMonth(report.Summary.ReportMonth)} nominal tutarı",
                    ReportKpiTone.Brand),
                CreateRateKpi(
                    "Nominal Varlık Değişimi",
                    report.Summary.NominalAssetChangeRate),
                CreateRateKpi(
                    "Dolarizasyon Varlık Değişimi",
                    report.Summary.DollarizedAssetChangeRate),
                CreateRateKpi(
                    "Enflasyon Varlık Değişimi",
                    report.Summary.InflationAdjustedAssetChangeRate)
            ],
            Rows: report.Rows
                .Select((row, index) => new ReportRowViewModel(
                    Month: FormatMonth(row.Month),
                    AssetValue: FormatMoney(row.AssetAmount),
                    MonthlyAssetIncreaseRate: index == 0
                        ? "—"
                        : FormatPercentage(row.MonthlyAssetChangeRate),
                    AssetChangeRate: FormatPercentage(row.AssetChangeRate),
                    UsdRate: FormatNumber(row.UsdRate, 4),
                    DollarizedAmount: FormatMoney(row.DollarizedAmount),
                    MonthlyDollarizedIncreaseRate: index == 0
                        ? "—"
                        : FormatPercentage(row.MonthlyDollarizedChangeRate),
                    DollarizedChangeRate: FormatPercentage(row.DollarizedChangeRate),
                    DollarizationEffect: FormatPercentage(row.DollarizationEffectRate),
                    ProducerPriceIndex: FormatNumber(row.ProducerPriceIndex, 2),
                    InflationAdjustedAmount: FormatMoney(row.InflationAdjustedAmount),
                    MonthlyInflationAdjustedIncreaseRate: index == 0
                        ? "—"
                        : FormatPercentage(row.MonthlyInflationAdjustedChangeRate),
                    InflationAdjustedChangeRate: FormatPercentage(row.InflationAdjustedChangeRate),
                    InflationEffect: FormatPercentage(row.InflationEffectRate)))
                .ToArray());
    }

    private static ReportKpiViewModel CreateRateKpi(
        string label,
        decimal? rate)
    {
        var tone = rate switch
        {
            > 0m => ReportKpiTone.Positive,
            < 0m => ReportKpiTone.Negative,
            _ => ReportKpiTone.Brand
        };

        return new(
            label,
            FormatPercentage(rate),
            "İlk aydan rapor ayına",
            tone);
    }

    private static string FormatMonth(DateOnly month)
    {
        var formatted = month.ToDateTime(TimeOnly.MinValue)
            .ToString("MMMM yyyy", TurkishCulture);

        return TurkishCulture.TextInfo.ToTitleCase(formatted);
    }

    private static string FormatMoney(decimal value) =>
        $"₺{FormatNumber(value, 2)}";

    private static string FormatPercentage(decimal? value)
    {
        if (!value.HasValue)
        {
            return "—";
        }

        var percentage = value.Value * 100m;

        return percentage switch
        {
            > 0m => $"+%{FormatNumber(percentage, 2)}",
            < 0m => $"-%{FormatNumber(decimal.Abs(percentage), 2)}",
            _ => "%0,00"
        };
    }

    private static string FormatNumber(decimal value, int decimalPlaces) =>
        value.ToString($"N{decimalPlaces}", TurkishCulture);
}
