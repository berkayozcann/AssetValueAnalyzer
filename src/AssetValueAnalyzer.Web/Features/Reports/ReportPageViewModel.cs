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
                new("Dolarizasyon Varlık Değişimi", "-%12,40", "İlk aydan rapor ayına", ReportKpiTone.Negative),
                new("Enflasyon Varlık Değişimi", "-%28,65", "İlk aydan rapor ayına", ReportKpiTone.Negative)
            ],
            Rows:
            [
                new("Ara 2021", "₺1.500.000", "—", "+%64,00", "13,60", "₺2.040.000", "—", "-%12,40", "-%26,47", "1.022,25", "₺3.818.075", "—", "-%28,65", "-%60,71"),
                new("Oca 2022", "₺1.575.000", "+%5,00", "+%56,19", "13,60", "₺2.142.000", "+%5,00", "-%16,57", "-%26,47", "1.129,64", "₺3.702.836", "-%3,02", "-%26,42", "-%57,47"),
                new("Şub 2022", "₺1.655.000", "+%5,08", "+%48,64", "13,89", "₺2.300.500", "+%7,40", "-%22,31", "-%28,06", "1.138,04", "₺3.862.542", "+%4,31", "-%29,46", "-%57,15"),
                new("Mar 2022", "₺1.740.000", "+%5,14", "+%41,38", "14,68", "₺2.554.200", "+%11,03", "-%29,99", "-%31,88", "1.144,97", "₺4.036.841", "+%4,51", "-%32,55", "-%56,90"),
                new("Nis 2022", "₺1.805.000", "+%3,74", "+%36,29", "14,74", "₺2.662.700", "+%4,25", "-%32,84", "-%32,21", "1.215,24", "₺3.866.130", "-%4,23", "-%29,57", "-%53,31"),
                new("May 2022", "₺1.880.000", "+%4,16", "+%30,85", "15,46", "₺2.904.800", "+%9,09", "-%38,44", "-%35,28", "1.252,32", "₺3.742.440", "-%3,20", "-%27,25", "-%49,77"),
                new("Haz 2022", "₺1.960.000", "+%4,26", "+%25,51", "16,71", "₺3.275.000", "+%12,74", "-%45,38", "-%40,15", "1.373,82", "₺3.629.551", "-%3,02", "-%24,77", "-%46,00"),
                new("Tem 2022", "₺2.030.000", "+%3,57", "+%21,18", "17,48", "₺3.547.200", "+%8,31", "-%49,57", "-%42,77", "1.443,88", "₺3.578.109", "-%1,42", "-%23,69", "-%43,27"),
                new("Ağu 2022", "₺2.110.000", "+%3,94", "+%16,59", "18,25", "₺3.849.300", "+%8,52", "-%53,52", "-%45,18", "1.432,74", "₺3.742.384", "+%4,59", "-%27,04", "-%43,62"),
                new("Eyl 2022", "₺2.180.000", "+%3,32", "+%12,84", "18,58", "₺4.050.400", "+%5,22", "-%55,83", "-%46,18", "1.515,13", "₺3.723.161", "-%0,51", "-%26,66", "-%41,45"),
                new("Eki 2022", "₺2.220.000", "+%1,83", "+%10,81", "18,61", "₺4.131.420", "+%2,00", "-%56,70", "-%46,27", "1.561,40", "₺3.690.000", "-%0,89", "-%26,00", "-%39,84"),
                new("Kas 2022", "₺2.260.000", "+%1,80", "+%8,85", "18,64", "₺4.212.640", "+%1,97", "-%57,53", "-%46,35", "1.669,30", "₺3.520.000", "-%4,61", "-%22,45", "-%35,80"),
                new("Ara 2022", "₺2.300.000", "+%1,77", "+%6,96", "18,70", "₺4.301.000", "+%2,10", "-%58,40", "-%46,52", "1.738,80", "₺3.450.000", "-%1,99", "-%20,87", "-%33,33"),
                new("Oca 2023", "₺2.340.000", "+%1,74", "+%5,13", "18,79", "₺4.396.860", "+%2,23", "-%59,31", "-%46,78", "1.865,10", "₺3.260.000", "-%5,51", "-%16,56", "-%28,22"),
                new("Şub 2023", "₺2.370.000", "+%1,28", "+%3,80", "18,88", "₺4.474.560", "+%1,77", "-%60,01", "-%47,03", "1.923,60", "₺3.190.000", "-%2,15", "-%14,73", "-%25,71"),
                new("Mar 2023", "₺2.390.000", "+%0,84", "+%2,93", "19,08", "₺4.560.120", "+%1,91", "-%60,76", "-%47,59", "2.041,40", "₺3.020.000", "-%5,33", "-%11,59", "-%20,86"),
                new("Nis 2023", "₺2.410.000", "+%0,84", "+%2,07", "19,39", "₺4.673.990", "+%2,50", "-%61,72", "-%48,44", "2.164,90", "₺2.870.000", "-%4,97", "-%7,32", "-%16,03"),
                new("May 2023", "₺2.430.000", "+%0,83", "+%1,23", "20,69", "₺5.027.670", "+%7,57", "-%64,42", "-%51,67", "2.179,00", "₺2.850.000", "-%0,70", "-%6,67", "-%14,74"),
                new("Haz 2023", "₺2.440.000", "+%0,41", "+%0,82", "23,37", "₺5.702.280", "+%13,42", "-%68,64", "-%57,21", "2.320,70", "₺2.690.000", "-%5,61", "-%3,72", "-%9,29"),
                new("Tem 2023", "₺2.450.000", "+%0,41", "+%0,41", "25,45", "₺6.235.250", "+%9,35", "-%71,33", "-%60,71", "2.511,80", "₺2.550.000", "-%5,20", "-%1,18", "-%3,92"),
                new("Ağu 2023", "₺2.460.000", "+%0,41", "%0,00", "26,79", "₺6.590.340", "+%5,69", "%0,00", "-%62,67", "2.602,50", "₺2.460.000", "-%3,53", "%0,00", "%0,00")
            ]);
}

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
