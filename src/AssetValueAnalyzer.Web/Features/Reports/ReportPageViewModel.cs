using AssetValueAnalyzer.Web.Features.Shared;

namespace AssetValueAnalyzer.Web.Features.Reports;

public sealed record ReportPageViewModel(
    string Period,
    bool IsSample,
    ExchangeRateCardViewModel ExchangeRate,
    IReadOnlyList<ReportKpiViewModel> Kpis,
    IReadOnlyList<ReportRowViewModel> Rows)
{
    public static ReportPageViewModel CreateSample() =>
        new(
            Period: "Aralık 2021 – Ağustos 2023",
            IsSample: true,
            ExchangeRate: new ExchangeRateCardViewModel(
                FormattedRate: "41,2874",
                TrendText: "Artış",
                LastSyncText: "Tasarım önizlemesi",
                Trend: ExchangeRateTrend.Increased,
                IsDemo: true),
            Kpis:
            [
                new("Rapor Ayı Varlık Tutarı", "₺2.460.000", "Ağustos 2023 nominal tutarı", ReportKpiTone.Brand),
                new("Nominal Varlık Değişimi", "+%64,00", "İlk aydan rapor ayına", ReportKpiTone.Positive),
                new("Dolar Bazında Reel Değişim", "-%12,40", "Kur etkisi sonrası değişim", ReportKpiTone.Negative),
                new("Enflasyon Bazında Reel Değişim", "-%28,65", "Yİ-ÜFE etkisi sonrası değişim", ReportKpiTone.Negative)
            ],
            Rows:
            [
                new("Ara 2021", "₺1.500.000", "—", "—", "13,60", "₺2.040.000", "—", "-%20,00", "-%26,47", "1.022,25", "₺3.818.075", "—", "-%35,57", "-%60,71"),
                new("Oca 2022", "₺1.575.000", "₺75.000", "+%5,00", "13,60", "₺2.142.000", "₺102.000", "-%20,00", "-%26,47", "1.129,64", "₺3.702.836", "-₺115.239", "-%34,98", "-%57,47"),
                new("Şub 2022", "₺1.655.000", "₺80.000", "+%5,08", "13,89", "₺2.300.500", "₺158.500", "-%20,00", "-%28,06", "1.138,04", "₺3.862.542", "+₺159.706", "-%34,55", "-%57,15"),
                new("Mar 2022", "₺1.740.000", "₺85.000", "+%5,14", "14,68", "₺2.554.200", "₺253.700", "-%20,00", "-%31,88", "1.144,97", "₺4.036.841", "+₺174.299", "-%33,94", "-%56,90"),
                new("Nis 2022", "₺1.805.000", "₺65.000", "+%3,74", "14,74", "₺2.662.700", "₺108.500", "-%10,00", "-%32,21", "1.215,24", "₺3.866.130", "-₺170.711", "-%31,12", "-%53,31"),
                new("May 2022", "₺1.880.000", "₺75.000", "+%4,16", "15,46", "₺2.904.800", "₺242.100", "-%18,00", "-%35,28", "1.252,32", "₺3.742.440", "-₺123.690", "-%29,51", "-%49,77"),
                new("Haz 2022", "₺1.960.000", "₺80.000", "+%4,26", "16,71", "₺3.275.000", "₺370.200", "-%17,00", "-%40,15", "1.373,82", "₺3.629.551", "-₺112.889", "-%27,29", "-%46,00"),
                new("Tem 2022", "₺2.030.000", "₺70.000", "+%3,57", "17,48", "₺3.547.200", "₺272.200", "-%17,00", "-%42,77", "1.443,88", "₺3.578.109", "-₺51.442", "-%25,94", "-%43,27"),
                new("Ağu 2022", "₺2.110.000", "₺80.000", "+%3,94", "18,25", "₺3.849.300", "₺302.100", "-%16,00", "-%45,18", "1.432,74", "₺3.742.384", "+₺164.275", "-%23,85", "-%43,62"),
                new("Eyl 2022", "₺2.180.000", "₺70.000", "+%3,32", "18,58", "₺4.050.400", "₺201.100", "-%16,00", "-%46,18", "1.515,13", "₺3.723.161", "-₺19.223", "-%21,95", "-%41,45")
            ]);
}

public sealed record ReportKpiViewModel(string Label, string Value, string Description, ReportKpiTone Tone);

public enum ReportKpiTone
{
    Brand,
    Positive,
    Negative
}

public sealed record ReportRowViewModel(
    string Month,
    string AssetValue,
    string MonthlyAssetIncrease,
    string AssetChangeRate,
    string UsdRate,
    string DollarizedAmount,
    string MonthlyDollarizedIncrease,
    string DollarizedChangeRate,
    string DollarizationEffect,
    string ProducerPriceIndex,
    string InflationAdjustedAmount,
    string MonthlyInflationAdjustedIncrease,
    string InflationAdjustedChangeRate,
    string InflationEffect);
