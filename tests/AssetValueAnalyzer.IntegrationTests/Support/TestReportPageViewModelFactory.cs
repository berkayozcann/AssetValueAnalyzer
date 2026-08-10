using AssetValueAnalyzer.Web.Features.Reports;
using AssetValueAnalyzer.Web.Features.Shared;

namespace AssetValueAnalyzer.IntegrationTests.Support;

internal static class TestReportPageViewModelFactory
{
    public static ReportPageViewModel Create() =>
        new(
            "Aralık 2021 – Ocak 2022",
            new ExchangeRateCardViewModel(
                "47,2500",
                "Artış",
                "Son kontrol · 08.08.2026 12:00",
                ExchangeRateTrend.Increased),
            [new ReportKpiViewModel(
                "Rapor Ayı Varlık Tutarı",
                "₺1.100,00",
                "Ocak 2022 ayındaki nominal varlık tutarı.",
                ReportKpiTone.Brand,
                ReportKpiIcon.AssetAmount)],
            [],
            new DateOnly(2021, 12, 1),
            new DateOnly(2022, 1, 1),
            new DateOnly(2021, 12, 1),
            new DateOnly(2022, 1, 1));
}
