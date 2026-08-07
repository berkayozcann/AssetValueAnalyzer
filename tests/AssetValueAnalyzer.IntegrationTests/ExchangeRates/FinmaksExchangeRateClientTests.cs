using System.Net;
using AssetValueAnalyzer.Infrastructure.Integrations.Finmaks;
using Microsoft.Extensions.Options;

namespace AssetValueAnalyzer.IntegrationTests.ExchangeRates;

public sealed class FinmaksExchangeRateClientTests
{
    [Fact]
    public async Task GetRatesAsync_MapsCashChangeRateAndDateRange()
    {
        const string responseJson = """
            {
              "ExchangeRates": [
                {
                  "BaseCurrencyCode": 1,
                  "ForeignCurrencyCode": 56,
                  "ChangeRate": 46.87830,
                  "ExchangeRate": 48.42890,
                  "CashChangeRate": 46.55073,
                  "CashExchangeRate": 48.78997,
                  "CentralBankChangeRate": 47.48810,
                  "CentralBankExchangeRate": 47.57360,
                  "CrossRate": 1.00000,
                  "CurrentDate": "2026-08-06T06:04:36"
                }
              ],
              "Header": {
                "Status": 0,
                "ResponseCode": "0000",
                "ResponseMessage": "İşlem başarılı."
              }
            }
            """;

        var handler = new StubHttpMessageHandler(responseJson);
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://testapi.finmaks.com/")
        };
        var options = Options.Create(new FinmaksOptions
        {
            ApiKey = "test-key"
        });
        var client = new FinmaksExchangeRateClient(httpClient, options);

        var rates = await client.GetRatesAsync(
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 6),
            CancellationToken.None);

        var rate = Assert.Single(rates);
        Assert.Equal(1, rate.BaseCurrencyCode);
        Assert.Equal(56, rate.ForeignCurrencyCode);
        Assert.Equal(46.55073m, rate.CashChangeRate);
        Assert.Equal(48.42890m, rate.ExchangeRateValue);
        Assert.Equal(new DateTime(2026, 8, 6, 6, 4, 36), rate.SourceUpdatedAt);

        Assert.Equal(
            "https://testapi.finmaks.com/ExchangeRates?key=test-key&startDate=2026-08-01&endDate=2026-08-06",
            handler.LastRequestUri?.ToString());
    }

    private sealed class StubHttpMessageHandler(string responseJson) : HttpMessageHandler
    {
        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson)
            });
        }
    }
}
