namespace AssetValueAnalyzer.Application.ExchangeRates.External;

public interface IFinmaksExchangeRateClient
{
    Task<IReadOnlyList<ExchangeRateQuote>> GetRatesAsync(
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        CancellationToken cancellationToken = default);
}
