namespace AssetValueAnalyzer.Infrastructure.BackgroundJobs;

public sealed class ExchangeRateRecurringJobOptions
{
    public const string SectionName = "ExchangeRateRecurringJob";
    public const int DefaultIntervalMinutes = 3;

    public bool Enabled { get; init; }

    public int IntervalMinutes { get; init; } = DefaultIntervalMinutes;
}
