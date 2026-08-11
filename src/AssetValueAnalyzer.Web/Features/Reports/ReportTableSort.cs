namespace AssetValueAnalyzer.Web.Features.Reports;

public enum ReportSortColumn
{
    Month,
    AssetValue,
    MonthlyAssetIncreaseRate,
    AssetChangeRate,
    UsdRate,
    DollarizedAmount,
    MonthlyDollarizedIncreaseRate,
    DollarizedChangeRate,
    DollarizationEffect,
    ProducerPriceIndex,
    InflationAdjustedAmount,
    MonthlyInflationAdjustedIncreaseRate,
    InflationAdjustedChangeRate,
    InflationEffect
}

public enum ReportSortDirection
{
    Ascending,
    Descending
}

public static class ReportTableSort
{
    private static readonly IReadOnlyDictionary<string, ReportSortColumn> Columns =
        new Dictionary<string, ReportSortColumn>(StringComparer.OrdinalIgnoreCase)
        {
            ["month"] = ReportSortColumn.Month,
            ["asset-value"] = ReportSortColumn.AssetValue,
            ["monthly-asset-increase"] = ReportSortColumn.MonthlyAssetIncreaseRate,
            ["asset-change"] = ReportSortColumn.AssetChangeRate,
            ["usd-rate"] = ReportSortColumn.UsdRate,
            ["dollarized-amount"] = ReportSortColumn.DollarizedAmount,
            ["monthly-dollarized-increase"] = ReportSortColumn.MonthlyDollarizedIncreaseRate,
            ["dollarized-change"] = ReportSortColumn.DollarizedChangeRate,
            ["dollarization-effect"] = ReportSortColumn.DollarizationEffect,
            ["producer-price-index"] = ReportSortColumn.ProducerPriceIndex,
            ["inflation-adjusted-amount"] = ReportSortColumn.InflationAdjustedAmount,
            ["monthly-inflation-adjusted-increase"] = ReportSortColumn.MonthlyInflationAdjustedIncreaseRate,
            ["inflation-adjusted-change"] = ReportSortColumn.InflationAdjustedChangeRate,
            ["inflation-effect"] = ReportSortColumn.InflationEffect
        };

    public static ReportPageViewModel Apply(
        ReportPageViewModel report,
        string? columnValue,
        string? directionValue)
    {
        ArgumentNullException.ThrowIfNull(report);

        var column = ParseColumn(columnValue);
        var direction = ParseDirection(directionValue);
        var rows = SortRows(report.Rows, column, direction).ToArray();

        return report with
        {
            Rows = rows,
            SortColumn = column,
            SortDirection = direction
        };
    }

    public static string ToQueryValue(ReportSortColumn column) =>
        Columns.Single(pair => pair.Value == column).Key;

    private static ReportSortColumn ParseColumn(string? value) =>
        value is not null && Columns.TryGetValue(value, out var column)
            ? column
            : ReportSortColumn.Month;

    private static ReportSortDirection ParseDirection(string? value) =>
        string.Equals(value, "desc", StringComparison.OrdinalIgnoreCase)
            ? ReportSortDirection.Descending
            : ReportSortDirection.Ascending;

    private static IEnumerable<ReportRowViewModel> SortRows(
        IReadOnlyList<ReportRowViewModel> rows,
        ReportSortColumn column,
        ReportSortDirection direction) =>
        column switch
        {
            ReportSortColumn.Month => Sort<DateOnly>(rows, row => row.SortValues.Month, direction),
            ReportSortColumn.AssetValue => Sort<decimal>(rows, row => row.SortValues.AssetValue, direction),
            ReportSortColumn.MonthlyAssetIncreaseRate => Sort<decimal>(rows, row => row.SortValues.MonthlyAssetIncreaseRate, direction),
            ReportSortColumn.AssetChangeRate => Sort<decimal>(rows, row => row.SortValues.AssetChangeRate, direction),
            ReportSortColumn.UsdRate => Sort<decimal>(rows, row => row.SortValues.UsdRate, direction),
            ReportSortColumn.DollarizedAmount => Sort<decimal>(rows, row => row.SortValues.DollarizedAmount, direction),
            ReportSortColumn.MonthlyDollarizedIncreaseRate => Sort<decimal>(rows, row => row.SortValues.MonthlyDollarizedIncreaseRate, direction),
            ReportSortColumn.DollarizedChangeRate => Sort<decimal>(rows, row => row.SortValues.DollarizedChangeRate, direction),
            ReportSortColumn.DollarizationEffect => Sort<decimal>(rows, row => row.SortValues.DollarizationEffect, direction),
            ReportSortColumn.ProducerPriceIndex => Sort<decimal>(rows, row => row.SortValues.ProducerPriceIndex, direction),
            ReportSortColumn.InflationAdjustedAmount => Sort<decimal>(rows, row => row.SortValues.InflationAdjustedAmount, direction),
            ReportSortColumn.MonthlyInflationAdjustedIncreaseRate => Sort<decimal>(rows, row => row.SortValues.MonthlyInflationAdjustedIncreaseRate, direction),
            ReportSortColumn.InflationAdjustedChangeRate => Sort<decimal>(rows, row => row.SortValues.InflationAdjustedChangeRate, direction),
            ReportSortColumn.InflationEffect => Sort<decimal>(rows, row => row.SortValues.InflationEffect, direction),
            _ => rows
        };

    private static IEnumerable<ReportRowViewModel> Sort<T>(
        IEnumerable<ReportRowViewModel> rows,
        Func<ReportRowViewModel, T?> valueSelector,
        ReportSortDirection direction)
        where T : struct, IComparable<T>
    {
        var nonNullValuesFirst = rows.OrderBy(row => valueSelector(row) is null);

        return direction == ReportSortDirection.Ascending
            ? nonNullValuesFirst.ThenBy(valueSelector)
            : nonNullValuesFirst.ThenByDescending(valueSelector);
    }
}
