namespace AssetValueAnalyzer.Application.ProducerPriceIndices.Imports;

public sealed record MonthlyProducerPriceIndexInput(
    DateOnly Month,
    decimal Value);

public sealed record ProducerPriceIndexImportValidationError(
    string Code,
    string Message,
    int? RowNumber = null,
    int? ColumnNumber = null);

public sealed record ProducerPriceIndexFileParseResult(
    IReadOnlyList<MonthlyProducerPriceIndexInput> Values,
    IReadOnlyList<ProducerPriceIndexImportValidationError> Errors)
{
    public bool IsValid => Errors.Count == 0;
}

public sealed record ProducerPriceIndexFileValidationResult(
    bool IsValid,
    int ParsedCount,
    DateOnly? FirstMonth,
    DateOnly? LastMonth,
    IReadOnlyList<ProducerPriceIndexImportValidationError> Errors)
{
    public static ProducerPriceIndexFileValidationResult FromParseResult(
        ProducerPriceIndexFileParseResult parseResult)
    {
        DateOnly? firstMonth = parseResult.Values.Count == 0
            ? null
            : parseResult.Values.Min(value => value.Month);
        DateOnly? lastMonth = parseResult.Values.Count == 0
            ? null
            : parseResult.Values.Max(value => value.Month);

        return new ProducerPriceIndexFileValidationResult(
            parseResult.IsValid,
            parseResult.Values.Count,
            firstMonth,
            lastMonth,
            parseResult.Errors);
    }

    public static ProducerPriceIndexFileValidationResult Invalid(
        params ProducerPriceIndexImportValidationError[] errors) =>
        new(false, 0, null, null, errors);
}
