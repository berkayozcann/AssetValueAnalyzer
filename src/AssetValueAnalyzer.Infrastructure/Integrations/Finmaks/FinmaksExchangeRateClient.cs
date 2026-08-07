using System.Globalization;
using System.Net.Http.Json;
using AssetValueAnalyzer.Application.ExchangeRates.External;
using AssetValueAnalyzer.Infrastructure.Integrations.Finmaks.Contracts;
using Microsoft.Extensions.Options;

namespace AssetValueAnalyzer.Infrastructure.Integrations.Finmaks;

public sealed class FinmaksExchangeRateClient(
    HttpClient httpClient,
    IOptions<FinmaksOptions> options) : IFinmaksExchangeRateClient
{
    private readonly FinmaksOptions _options = options.Value;

    public async Task<IReadOnlyList<ExchangeRateQuote>> GetRatesAsync(
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(startDate, endDate);

        using var response = await httpClient.GetAsync(
            BuildRequestUri(startDate, endDate),
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<FinmaksExchangeRatesResponse>(
            cancellationToken: cancellationToken);

        if (payload is null)
        {
            throw new FinmaksApiException("Finmaks returned an empty response.");
        }

        if (payload.Header.Status != 0 || payload.Header.ResponseCode != "0000")
        {
            throw new FinmaksApiException(
                $"Finmaks rejected the request with response code {payload.Header.ResponseCode}.");
        }

        return payload.ExchangeRates
            .Select(rate => new ExchangeRateQuote(
                rate.BaseCurrencyCode,
                rate.ForeignCurrencyCode,
                rate.ChangeRate,
                rate.ExchangeRateValue,
                rate.CashChangeRate,
                rate.CashExchangeRate,
                rate.CentralBankChangeRate,
                rate.CentralBankExchangeRate,
                rate.CrossRate,
                rate.SourceUpdatedAt))
            .ToArray();
    }

    private Uri BuildRequestUri(DateOnly? startDate, DateOnly? endDate)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException("The Finmaks API key has not been configured.");
        }

        var queryParts = new List<string>
        {
            $"key={Uri.EscapeDataString(_options.ApiKey)}"
        };

        if (startDate.HasValue && endDate.HasValue)
        {
            queryParts.Add($"startDate={FormatDate(startDate.Value)}");
            queryParts.Add($"endDate={FormatDate(endDate.Value)}");
        }

        return new Uri($"ExchangeRates?{string.Join('&', queryParts)}", UriKind.Relative);
    }

    private static void ValidateRequest(DateOnly? startDate, DateOnly? endDate)
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

    private static string FormatDate(DateOnly date) =>
        date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
