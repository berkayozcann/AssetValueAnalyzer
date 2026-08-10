using System.Globalization;
using AssetValueAnalyzer.Application.ExchangeRates.Queries;

namespace AssetValueAnalyzer.Web.Features.Shared;

public static class ExchangeRateCardViewModelFactory
{
    private static readonly CultureInfo TurkishCulture = CultureInfo.GetCultureInfo("tr-TR");

    public static ExchangeRateCardViewModel Create(CurrentUsdExchangeRate? rate)
    {
        if (rate is null)
        {
            return new(
                FormattedRate: "—",
                TrendText: "Karşılaştırma için önceki kur verisi bulunmuyor.",
                LastSyncText: "Kur verisi henüz alınmadı.",
                Trend: ExchangeRateTrend.Unavailable,
                HasRate: false);
        }

        var trend = rate.PreviousValue switch
        {
            null => ExchangeRateTrend.Unavailable,
            var previous when rate.Value > previous => ExchangeRateTrend.Increased,
            var previous when rate.Value < previous => ExchangeRateTrend.Decreased,
            _ => ExchangeRateTrend.Unchanged
        };

        return new(
            FormattedRate: rate.Value.ToString("N4", TurkishCulture),
            TrendText: trend switch
            {
                ExchangeRateTrend.Increased => "USD/TRY kuru bir önceki kur gününe göre yükseldi.",
                ExchangeRateTrend.Decreased => "USD/TRY kuru bir önceki kur gününe göre düştü.",
                ExchangeRateTrend.Unchanged => "USD/TRY kuru bir önceki kur gününe göre değişmedi.",
                _ => "Karşılaştırma için önceki kur verisi bulunmuyor."
            },
            LastSyncText: $"Son kontrol · {rate.RetrievedAtUtc.ToLocalTime():dd.MM.yyyy HH:mm}",
            Trend: trend);
    }
}
