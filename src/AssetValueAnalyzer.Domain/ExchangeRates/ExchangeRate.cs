namespace AssetValueAnalyzer.Domain.ExchangeRates;

public sealed class ExchangeRate
{
    private ExchangeRate()
    {
    }

    public ExchangeRate(
        int baseCurrencyCode,
        int foreignCurrencyCode,
        DateTime sourceUpdatedAt,
        DateTimeOffset retrievedAtUtc,
        decimal changeRate,
        decimal exchangeRateValue,
        decimal cashChangeRate,
        decimal cashExchangeRate,
        decimal centralBankChangeRate,
        decimal centralBankExchangeRate,
        decimal crossRate)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(baseCurrencyCode);
        ArgumentOutOfRangeException.ThrowIfNegative(foreignCurrencyCode);

        BaseCurrencyCode = baseCurrencyCode;
        ForeignCurrencyCode = foreignCurrencyCode;
        RateDate = GetRateDate(sourceUpdatedAt);

        UpdateRates(
            sourceUpdatedAt,
            retrievedAtUtc,
            changeRate,
            exchangeRateValue,
            cashChangeRate,
            cashExchangeRate,
            centralBankChangeRate,
            centralBankExchangeRate,
            crossRate);
    }

    public long Id { get; private set; }

    public int BaseCurrencyCode { get; private set; }

    public int ForeignCurrencyCode { get; private set; }

    public DateOnly RateDate { get; private set; }

    public decimal ChangeRate { get; private set; }

    public decimal ExchangeRateValue { get; private set; }

    public decimal CashChangeRate { get; private set; }

    public decimal CashExchangeRate { get; private set; }

    public decimal CentralBankChangeRate { get; private set; }

    public decimal CentralBankExchangeRate { get; private set; }

    public decimal CrossRate { get; private set; }

    public DateTime SourceUpdatedAt { get; private set; }

    public DateTimeOffset RetrievedAtUtc { get; private set; }

    public void UpdateRates(
        DateTime sourceUpdatedAt,
        DateTimeOffset retrievedAtUtc,
        decimal changeRate,
        decimal exchangeRateValue,
        decimal cashChangeRate,
        decimal cashExchangeRate,
        decimal centralBankChangeRate,
        decimal centralBankExchangeRate,
        decimal crossRate)
    {
        var incomingRateDate = GetRateDate(sourceUpdatedAt);

        if (incomingRateDate != RateDate)
        {
            throw new ArgumentException(
                "An exchange-rate record cannot be updated with a different rate date.",
                nameof(sourceUpdatedAt));
        }

        EnsureNonNegative(changeRate, nameof(changeRate));
        EnsureNonNegative(exchangeRateValue, nameof(exchangeRateValue));
        EnsurePositive(cashChangeRate, nameof(cashChangeRate));
        EnsureNonNegative(cashExchangeRate, nameof(cashExchangeRate));
        EnsureNonNegative(centralBankChangeRate, nameof(centralBankChangeRate));
        EnsureNonNegative(centralBankExchangeRate, nameof(centralBankExchangeRate));
        EnsureNonNegative(crossRate, nameof(crossRate));

        SourceUpdatedAt = sourceUpdatedAt;
        RetrievedAtUtc = retrievedAtUtc.ToUniversalTime();
        ChangeRate = changeRate;
        ExchangeRateValue = exchangeRateValue;
        CashChangeRate = cashChangeRate;
        CashExchangeRate = cashExchangeRate;
        CentralBankChangeRate = centralBankChangeRate;
        CentralBankExchangeRate = centralBankExchangeRate;
        CrossRate = crossRate;
    }

    private static DateOnly GetRateDate(DateTime sourceUpdatedAt)
    {
        if (sourceUpdatedAt == default)
        {
            throw new ArgumentException(
                "The source update time must be provided.",
                nameof(sourceUpdatedAt));
        }

        return DateOnly.FromDateTime(sourceUpdatedAt);
    }

    private static void EnsurePositive(decimal value, string parameterName)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "Exchange-rate values must be greater than zero.");
        }
    }

    private static void EnsureNonNegative(decimal value, string parameterName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "Exchange-rate values cannot be negative.");
        }
    }
}
