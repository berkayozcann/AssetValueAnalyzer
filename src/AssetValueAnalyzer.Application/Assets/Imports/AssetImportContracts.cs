namespace AssetValueAnalyzer.Application.Assets.Imports;

public static class AssetImportRules
{
    public static DateOnly EarliestSupportedMonth { get; } = new(2021, 12, 1);
}

public sealed record MonthlyAssetValueInput(
    DateOnly Month,
    decimal Amount);

public sealed record AssetImportValidationError(
    string Code,
    string Message,
    int? RowNumber = null);

public sealed record AssetFileParseResult(
    IReadOnlyList<MonthlyAssetValueInput> Values,
    IReadOnlyList<AssetImportValidationError> Errors)
{
    public bool IsValid => Errors.Count == 0;
}

public sealed record AssetFileValidationResult(
    bool IsValid,
    int ParsedCount,
    DateOnly? FirstMonth,
    DateOnly? LastMonth,
    IReadOnlyList<AssetImportValidationError> Errors)
{
    public static AssetFileValidationResult FromParseResult(
        AssetFileParseResult parseResult)
    {
        DateOnly? firstMonth = parseResult.Values.Count == 0
            ? null
            : parseResult.Values.Min(value => value.Month);
        DateOnly? lastMonth = parseResult.Values.Count == 0
            ? null
            : parseResult.Values.Max(value => value.Month);

        return new AssetFileValidationResult(
            parseResult.IsValid,
            parseResult.Values.Count,
            firstMonth,
            lastMonth,
            parseResult.Errors);
    }

    public static AssetFileValidationResult Invalid(
        params AssetImportValidationError[] errors) =>
        new(false, 0, null, null, errors);
}
