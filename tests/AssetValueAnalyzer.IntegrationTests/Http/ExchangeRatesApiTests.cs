using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AssetValueAnalyzer.IntegrationTests.Support;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AssetValueAnalyzer.IntegrationTests.Http;

public sealed class ExchangeRatesApiTests(
    AssetValueAnalyzerApiApplicationFactory factory)
    : IClassFixture<AssetValueAnalyzerApiApplicationFactory>
{
    [Fact]
    public async Task GetLatest_WithoutFilters_ReturnsLatestDateDtosWithoutEntityId()
    {
        using var client = CreateClient();

        var response = await client.GetAsync("/api/exchange-rates/latest");
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, json.RootElement.GetArrayLength());
        Assert.All(json.RootElement.EnumerateArray(), rate =>
        {
            Assert.Equal("2026-08-09", rate.GetProperty("rateDate").GetString());
            Assert.False(rate.TryGetProperty("id", out _));
            Assert.True(rate.TryGetProperty("cashChangeRate", out _));
        });
    }

    [Fact]
    public async Task GetLatest_WithDateCurrencyAndLimitFilters_ReturnsMatchingRate()
    {
        using var client = CreateClient();

        var response = await client.GetAsync(
            "/api/exchange-rates/latest?rateDate=2026-08-08&baseCurrencyCode=1&foreignCurrencyCode=56&limit=1");
        var rates = await response.Content.ReadFromJsonAsync<ExchangeRateApiResponse[]>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rate = Assert.Single(Assert.IsType<ExchangeRateApiResponse[]>(rates));
        Assert.Equal(1, rate.BaseCurrencyCode);
        Assert.Equal(56, rate.ForeignCurrencyCode);
        Assert.Equal(new DateOnly(2026, 8, 8), rate.RateDate);
        Assert.Equal(45.75m, rate.CashChangeRate);
    }

    [Fact]
    public async Task GetLatest_WithNoMatchingRates_ReturnsProblemDetailsNotFound()
    {
        using var client = CreateClient();

        var response = await client.GetAsync(
            "/api/exchange-rates/latest?rateDate=2020-01-01");
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(problem);
        Assert.Equal(404, problem.Status);
        Assert.Equal("Exchange rates not found", problem.Title);
    }

    [Fact]
    public async Task GetLatest_WithInvalidLimit_ReturnsValidationProblemDetails()
    {
        using var client = CreateClient();

        var response = await client.GetAsync(
            "/api/exchange-rates/latest?limit=0");
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(problem);
        Assert.Equal(400, problem.Status);
        Assert.Contains("Limit", problem.Errors.Keys);
    }

    [Fact]
    public async Task GetLatest_WhenReaderThrows_ReturnsGenericProblemDetails()
    {
        using var client = CreateClient();

        var response = await client.GetAsync(
            "/api/exchange-rates/latest?baseCurrencyCode=999");
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(problem);
        Assert.Equal(500, problem.Status);
        Assert.DoesNotContain("Controlled API test failure", problem.Detail ?? string.Empty);
    }

    [Fact]
    public async Task GetRange_WithDatesCurrencyFiltersAndLimit_ReturnsOrderedRates()
    {
        using var client = CreateClient();

        var response = await client.GetAsync(
            "/api/exchange-rates?startDate=2026-08-08&endDate=2026-08-09&baseCurrencyCode=1&foreignCurrencyCode=56&limit=2");
        var rates = await response.Content.ReadFromJsonAsync<ExchangeRateApiResponse[]>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = Assert.IsType<ExchangeRateApiResponse[]>(rates);
        Assert.Collection(
            result,
            rate => Assert.Equal(new DateOnly(2026, 8, 9), rate.RateDate),
            rate => Assert.Equal(new DateOnly(2026, 8, 8), rate.RateDate));
        Assert.All(result, rate =>
        {
            Assert.Equal(1, rate.BaseCurrencyCode);
            Assert.Equal(56, rate.ForeignCurrencyCode);
        });
    }

    [Fact]
    public async Task GetRange_WithoutEndDate_ReturnsValidationProblemDetails()
    {
        using var client = CreateClient();

        var response = await client.GetAsync(
            "/api/exchange-rates?startDate=2026-08-08");
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Contains("EndDate", problem.Errors.Keys);
    }

    [Fact]
    public async Task GetRange_WithStartDateAfterEndDate_ReturnsValidationProblemDetails()
    {
        using var client = CreateClient();

        var response = await client.GetAsync(
            "/api/exchange-rates?startDate=2026-08-10&endDate=2026-08-09");
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Contains("StartDate", problem.Errors.Keys);
        Assert.Contains("EndDate", problem.Errors.Keys);
    }

    [Fact]
    public async Task GetRange_WithNoMatchingRates_ReturnsProblemDetailsNotFound()
    {
        using var client = CreateClient();

        var response = await client.GetAsync(
            "/api/exchange-rates?startDate=2020-01-01&endDate=2020-01-02");
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(problem);
        Assert.Equal(404, problem.Status);
        Assert.Equal("Exchange rates not found", problem.Title);
    }

    private HttpClient CreateClient() => factory.CreateClient(
        new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

    private sealed record ExchangeRateApiResponse(
        int BaseCurrencyCode,
        int ForeignCurrencyCode,
        DateOnly RateDate,
        decimal CashChangeRate);
}
