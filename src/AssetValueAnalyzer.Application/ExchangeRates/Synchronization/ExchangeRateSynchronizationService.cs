using AssetValueAnalyzer.Application.ExchangeRates.External;
using AssetValueAnalyzer.Domain.ExchangeRates;

namespace AssetValueAnalyzer.Application.ExchangeRates.Synchronization;

public sealed class ExchangeRateSynchronizationService
{
    private readonly IFinmaksExchangeRateClient _finmaksClient;
    private readonly IExchangeRateStore _exchangeRateStore;
    private readonly TimeProvider _timeProvider;

    public ExchangeRateSynchronizationService(
        IFinmaksExchangeRateClient finmaksClient,
        IExchangeRateStore exchangeRateStore,
        TimeProvider timeProvider)
    {
        _finmaksClient = finmaksClient;
        _exchangeRateStore = exchangeRateStore;
        _timeProvider = timeProvider;
    }

    public async Task<SyncExchangeRatesResult> SynchronizeAsync(
        SyncExchangeRatesRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateDateRange(request.StartDate, request.EndDate);

        var quotes = await _finmaksClient.GetRatesAsync(
            request.StartDate,
            request.EndDate,
            cancellationToken);

        var retrievedAtUtc = _timeProvider.GetUtcNow();
        var exchangeRates = quotes
            .Select(quote => CreateExchangeRate(quote, retrievedAtUtc))
            .ToArray();

        var upsertResult = await _exchangeRateStore.UpsertAsync(
            exchangeRates,
            cancellationToken);

        return new SyncExchangeRatesResult(
            exchangeRates.Length,
            upsertResult.InsertedCount,
            upsertResult.UpdatedCount,
            upsertResult.UnchangedCount);
    }

    private static ExchangeRate CreateExchangeRate(
        ExchangeRateQuote quote,
        DateTimeOffset retrievedAtUtc) =>
        new(
            quote.BaseCurrencyCode,
            quote.ForeignCurrencyCode,
            quote.SourceUpdatedAt,
            retrievedAtUtc,
            quote.ChangeRate,
            quote.ExchangeRateValue,
            quote.CashChangeRate,
            quote.CashExchangeRate,
            quote.CentralBankChangeRate,
            quote.CentralBankExchangeRate,
            quote.CrossRate);

    private static void ValidateDateRange(DateOnly? startDate, DateOnly? endDate)
    {
        if (startDate.HasValue != endDate.HasValue)
        {
            throw new ArgumentException(
                "Start date and end date must either both be provided or both be omitted.");
        }

        if (startDate > endDate)
        {
            throw new ArgumentException("Start date cannot be later than end date.");
        }
    }
}
