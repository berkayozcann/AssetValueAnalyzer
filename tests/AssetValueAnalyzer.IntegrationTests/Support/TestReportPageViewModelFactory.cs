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
                "Son senkronizasyon: 08.08.2026 12:00",
                ExchangeRateTrend.Increased),
            [new ReportKpiViewModel("Rapor Ayı Varlık Tutarı", "₺1.100,00", "Ocak 2022 nominal tutarı", ReportKpiTone.Brand)],
            []);
}
