namespace AssetValueAnalyzer.Infrastructure.Persistence.ExchangeRates;

public sealed class ExchangeRateBackfillCheckpoint
{
    public const int SingletonId = 1;

    private ExchangeRateBackfillCheckpoint()
    {
    }

    public ExchangeRateBackfillCheckpoint(
        DateOnly completedThroughDate,
        DateTimeOffset completedAtUtc)
    {
        Id = SingletonId;
        MarkCompleted(completedThroughDate, completedAtUtc);
    }

    public int Id { get; private set; }

    public DateOnly CompletedThroughDate { get; private set; }

    public DateTimeOffset CompletedAtUtc { get; private set; }

    public void MarkCompleted(
        DateOnly completedThroughDate,
        DateTimeOffset completedAtUtc)
    {
        if (completedThroughDate == default)
        {
            throw new ArgumentException(
                "The completed-through date must be provided.",
                nameof(completedThroughDate));
        }

        if (completedAtUtc == default)
        {
            throw new ArgumentException(
                "The completion time must be provided.",
                nameof(completedAtUtc));
        }

        var normalizedCompletedAtUtc = completedAtUtc.ToUniversalTime();

        if (CompletedThroughDate > completedThroughDate ||
            (CompletedThroughDate == completedThroughDate &&
             CompletedAtUtc >= normalizedCompletedAtUtc))
        {
            return;
        }

        CompletedThroughDate = completedThroughDate;
        CompletedAtUtc = normalizedCompletedAtUtc;
    }
}
