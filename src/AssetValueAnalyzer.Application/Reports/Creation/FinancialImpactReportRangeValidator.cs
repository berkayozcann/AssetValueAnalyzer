using AssetValueAnalyzer.Application.Assets.Imports;
using AssetValueAnalyzer.Application.ProducerPriceIndices.Imports;
using System.Globalization;

namespace AssetValueAnalyzer.Application.Reports.Creation;

public sealed class FinancialImpactReportRangeValidator(
    TimeProvider timeProvider)
{
    private static readonly CultureInfo TurkishCulture =
        CultureInfo.GetCultureInfo("tr-TR");

    public FinancialImpactReportRangeValidationResult Validate(
        CreateFinancialImpactReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.AssetValues);
        ArgumentNullException.ThrowIfNull(request.ProducerPriceIndices);

        var assetValues = request.AssetValues
            .OrderBy(value => value.Month)
            .ToArray();
        var producerPriceIndices = request.ProducerPriceIndices
            .OrderBy(value => value.Month)
            .ToArray();
        var errors = ValidateInputs(assetValues, producerPriceIndices, request);

        if (errors.Count > 0)
        {
            return FinancialImpactReportRangeValidationResult.Invalid(errors);
        }

        var startMonth = request.StartMonth ?? assetValues[0].Month;
        var endMonth = request.EndMonth ?? assetValues[^1].Month;
        var currentDate = timeProvider.GetLocalNow();
        var currentMonth = new DateOnly(currentDate.Year, currentDate.Month, 1);

        if (endMonth >= currentMonth)
        {
            return FinancialImpactReportRangeValidationResult.Invalid(
            [
                new(
                    "IncompleteReportMonth",
                    $"Tamamlanmamış mevcut ay veya gelecek aylar rapora dahil edilemez. En geç {FormatMonth(currentMonth.AddMonths(-1))} seçilebilir.",
                    endMonth)
            ]);
        }

        var selectedAssetValues = assetValues
            .Where(value => value.Month >= startMonth && value.Month <= endMonth)
            .ToArray();

        if (selectedAssetValues.Length < 2)
        {
            return FinancialImpactReportRangeValidationResult.Invalid(
            [
                new(
                    "AtLeastTwoMonthsRequired",
                    "Rapor dönemi en az iki farklı varlık ayını içermelidir.")
            ]);
        }

        var indexMonths = producerPriceIndices
            .Select(value => value.Month)
            .ToHashSet();
        var missingIndexMonths = selectedAssetValues
            .Where(asset => !indexMonths.Contains(asset.Month))
            .Select(asset => asset.Month)
            .ToArray();

        if (missingIndexMonths.Length > 0)
        {
            var formattedMonths = string.Join(", ", missingIndexMonths.Select(FormatMonth));

            return FinancialImpactReportRangeValidationResult.Invalid(
            [
                new(
                    "MissingProducerPriceIndex",
                    $"Aylık Varlık Verisi bulunan her ay için Yİ-ÜFE Endeks Verisi bulunmalıdır. Eksik aylar: {formattedMonths}.",
                    missingIndexMonths[0])
            ]);
        }

        return FinancialImpactReportRangeValidationResult.Success(
            startMonth,
            endMonth,
            selectedAssetValues);
    }

    private static string FormatMonth(DateOnly month)
    {
        var formatted = month.ToString("MMMM yyyy", TurkishCulture);

        return TurkishCulture.TextInfo.ToTitleCase(formatted);
    }

    private static List<FinancialImpactReportCreationError> ValidateInputs(
        IReadOnlyList<MonthlyAssetValueInput> assetValues,
        IReadOnlyList<MonthlyProducerPriceIndexInput> producerPriceIndices,
        CreateFinancialImpactReportRequest request)
    {
        var errors = new List<FinancialImpactReportCreationError>();

        if (assetValues.Count == 0)
        {
            errors.Add(new(
                "MissingAssetValues",
                "Finansal etki analizi oluşturmak için geçerli bir Aylık Varlık Verisi dosyası yüklenmelidir."));
            return errors;
        }

        if (producerPriceIndices.Count == 0)
        {
            errors.Add(new(
                "MissingProducerPriceIndices",
                "Finansal etki analizi oluşturmak için geçerli bir Yİ-ÜFE Endeks Verisi dosyası yüklenmelidir."));
            return errors;
        }

        AddDuplicateMonthErrors(
            assetValues.Select(value => value.Month),
            "DuplicateAssetMonth",
            "Aylık Varlık Verisi",
            errors);
        AddDuplicateMonthErrors(
            producerPriceIndices.Select(value => value.Month),
            "DuplicateProducerPriceIndexMonth",
            "Yİ-ÜFE Endeks Verisi",
            errors);

        ValidateRequestedMonth(request.StartMonth, "Başlangıç", errors);
        ValidateRequestedMonth(request.EndMonth, "Bitiş", errors);

        if (request.StartMonth.HasValue &&
            request.EndMonth.HasValue &&
            request.StartMonth.Value > request.EndMonth.Value)
        {
            errors.Add(new(
                "InvalidDateRange",
                "Başlangıç ayı bitiş ayından sonra olamaz."));
        }

        if (request.StartMonth.HasValue &&
            !assetValues.Any(value => value.Month == request.StartMonth.Value))
        {
            errors.Add(new(
                "StartMonthNotFound",
                $"Seçilen başlangıç ayı ({request.StartMonth:yyyy-MM}) Aylık Varlık Verisi dosyasında bulunamadı.",
                request.StartMonth));
        }

        if (request.EndMonth.HasValue &&
            !assetValues.Any(value => value.Month == request.EndMonth.Value))
        {
            errors.Add(new(
                "EndMonthNotFound",
                $"Seçilen bitiş ayı ({request.EndMonth:yyyy-MM}) Aylık Varlık Verisi dosyasında bulunamadı.",
                request.EndMonth));
        }

        return errors;
    }

    private static void AddDuplicateMonthErrors(
        IEnumerable<DateOnly> months,
        string code,
        string dataSetName,
        ICollection<FinancialImpactReportCreationError> errors)
    {
        foreach (var duplicateMonth in months
                     .GroupBy(month => month)
                     .Where(group => group.Count() > 1)
                     .Select(group => group.Key))
        {
            errors.Add(new(
                code,
                $"{dataSetName} içinde {duplicateMonth:yyyy-MM} ayı birden fazla kez bulunuyor.",
                duplicateMonth));
        }
    }

    private static void ValidateRequestedMonth(
        DateOnly? month,
        string fieldName,
        ICollection<FinancialImpactReportCreationError> errors)
    {
        if (month.HasValue && month.Value.Day != 1)
        {
            errors.Add(new(
                "MonthMustBeNormalized",
                $"{fieldName} ayı, ayın ilk günüyle temsil edilmelidir.",
                month));
        }
    }
}

public sealed record FinancialImpactReportRangeValidationResult(
    DateOnly? EffectiveStartMonth,
    DateOnly? EffectiveEndMonth,
    IReadOnlyList<MonthlyAssetValueInput> SelectedAssetValues,
    IReadOnlyList<FinancialImpactReportCreationError> Errors)
{
    public bool IsValid => Errors.Count == 0;

    public static FinancialImpactReportRangeValidationResult Success(
        DateOnly startMonth,
        DateOnly endMonth,
        IReadOnlyList<MonthlyAssetValueInput> selectedAssetValues) =>
        new(startMonth, endMonth, selectedAssetValues, []);

    public static FinancialImpactReportRangeValidationResult Invalid(
        IReadOnlyList<FinancialImpactReportCreationError> errors) =>
        new(null, null, [], errors);
}
