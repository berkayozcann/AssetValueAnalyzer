using System.Globalization;

namespace AssetValueAnalyzer.Web.Features.Shared;

public static class MonthDisplayFormatter
{
    private static readonly CultureInfo TurkishCulture =
        CultureInfo.GetCultureInfo("tr-TR");

    public static string Format(DateOnly month) =>
        month.ToString("MMMM yyyy", TurkishCulture);

    public static string? FormatRange(
        DateOnly? firstMonth,
        DateOnly? lastMonth) =>
        firstMonth is null || lastMonth is null
            ? null
            : $"{Format(firstMonth.Value)} – {Format(lastMonth.Value)}";
}
