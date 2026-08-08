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
                TrendText: "Karşılaştırma yok",
                LastSyncText: "Henüz kur verisi bulunmuyor",
                Trend: ExchangeRateTrend.Unavailable);
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
                ExchangeRateTrend.Increased => "Artış",
                ExchangeRateTrend.Decreased => "Azalış",
                ExchangeRateTrend.Unchanged => "Değişmedi",
                _ => "Karşılaştırma yok"
            },
            LastSyncText: $"Son veri güncellemesi: {rate.RetrievedAtUtc.ToLocalTime():dd.MM.yyyy HH:mm}",
            Trend: trend);
    }
}
