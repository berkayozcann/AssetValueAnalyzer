using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using AssetValueAnalyzer.Application.ExchangeRates.Synchronization;
using AssetValueAnalyzer.IntegrationTests.Support;
using AssetValueAnalyzer.Web.Features.ExchangeRates.Realtime;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace AssetValueAnalyzer.IntegrationTests.Http;

public sealed partial class WebApplicationSmokeTests(
    AssetValueAnalyzerWebApplicationFactory factory)
    : IClassFixture<AssetValueAnalyzerWebApplicationFactory>
{
    [Fact]
    public void WebHost_UsesSignalRNotifierInsteadOfInfrastructureFallback()
    {
        using var scope = factory.Services.CreateScope();

        var notifier = scope.ServiceProvider
            .GetRequiredService<IExchangeRateSynchronizationNotifier>();

        Assert.IsType<SignalRExchangeRateSynchronizationNotifier>(notifier);
    }

    [Fact]
    public async Task HomePage_UsesRealPipelineAndReturnsAntiforgeryToken()
    {
        using var client = CreateClient();

        var response = await client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Yeni Rapor Oluştur", html);
        Assert.NotEmpty(ReadAntiforgeryToken(html));
        Assert.Contains("/lib/signalr/signalr.min.js", html);
    }

    [Fact]
    public async Task ExchangeRateCard_ReturnsRefreshablePartialWithoutPageLayout()
    {
        using var client = CreateClient();

        var response = await client.GetAsync("/exchange-rates/card");
        var html = await response.Content.ReadAsStringAsync();
        var decodedHtml = WebUtility.HtmlDecode(html);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("data-exchange-rate-card", html);
        Assert.Contains("Henüz kur verisi bulunmuyor", decodedHtml);
        Assert.DoesNotContain("<!DOCTYPE html>", html);
    }

    [Fact]
    public async Task ExchangeRateHub_NegotiateEndpoint_IsMapped()
    {
        using var client = CreateClient();

        var response = await client.PostAsync(
            "/hubs/exchange-rates/negotiate?negotiateVersion=1",
            content: null);
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(json.RootElement.TryGetProperty("connectionId", out var connectionId));
        Assert.False(string.IsNullOrWhiteSpace(connectionId.GetString()));
        Assert.True(json.RootElement.TryGetProperty("availableTransports", out _));
    }

    [Fact]
    public async Task UploadAssets_WithoutAntiforgeryToken_ReturnsBadRequest()
    {
        using var client = CreateClient();
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent([1]), "file", "varlik.xlsx");

        var response = await client.PostAsync("/imports/assets/validate", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadAssets_WithoutFile_ReturnsValidationContract()
    {
        using var client = CreateClient();
        var token = await GetAntiforgeryTokenAsync(client);
        using var content = new MultipartFormDataContent
        {
            { new StringContent(token), "__RequestVerificationToken" }
        };

        var response = await client.PostAsync("/imports/assets/validate", content);
        var result = await response.Content.ReadFromJsonAsync<AssetUploadResponse>();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.NotNull(result);
        Assert.False(result.IsValid);
        Assert.Equal("MissingFile", Assert.Single(result.Errors).Code);
    }

    [Fact]
    public async Task UploadAssets_WithValidXlsx_PersistsSessionAcrossRequests()
    {
        using var client = CreateClient();
        var token = await GetAntiforgeryTokenAsync(client);
        await using var stream = CreateAssetWorkbook();
        using var content = new MultipartFormDataContent
        {
            { new StringContent(token), "__RequestVerificationToken" }
        };
        using var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(fileContent, "file", "smoke-assets.xlsx");

        var response = await client.PostAsync("/imports/assets/validate", content);
        var result = await response.Content.ReadFromJsonAsync<AssetUploadResponse>();
        var homeHtml = await client.GetStringAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.True(result.IsValid);
        Assert.Equal(2, result.ParsedCount);
        Assert.Contains("smoke-assets.xlsx", homeHtml);
    }

    [Fact]
    public async Task CreateReport_WithTwoValidXlsxFiles_RendersCalculatedResultTable()
    {
        using var client = CreateClient();
        var token = await GetAntiforgeryTokenAsync(client);
        await using var assetWorkbook = CreateAssetWorkbook();
        await using var indexWorkbook = CreateProducerPriceIndexWorkbook();
        using var assetResponse = await UploadWorkbookAsync(
            client,
            "/imports/assets/validate",
            "smoke-assets.xlsx",
            assetWorkbook,
            token);
        using var indexResponse = await UploadWorkbookAsync(
            client,
            "/imports/indices/validate",
            "smoke-indices.xlsx",
            indexWorkbook,
            token);
        using var createContent = new FormUrlEncodedContent(
        [
            new("__RequestVerificationToken", token),
            new("StartMonth", "2021-12"),
            new("EndMonth", "2022-01")
        ]);

        var createResponse = await client.PostAsync("/reports/create", createContent);
        var reportResponse = await client.GetAsync("/reports");
        var reportHtml = await reportResponse.Content.ReadAsStringAsync();
        var decodedReportHtml = WebUtility.HtmlDecode(reportHtml);

        Assert.Equal(HttpStatusCode.OK, assetResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, indexResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, createResponse.StatusCode);
        Assert.Equal("/reports", createResponse.Headers.Location?.OriginalString);
        Assert.Equal(HttpStatusCode.OK, reportResponse.StatusCode);
        Assert.Contains("Finansal Etki Analizi Raporu", decodedReportHtml);
        Assert.Contains("Aralık 2021 – Ocak 2022", decodedReportHtml);
        Assert.Contains("₺2.000.000,00", decodedReportHtml);
        Assert.Equal(2, reportHtml.Split("data-report-row").Length - 1);
    }

    private HttpClient CreateClient() => factory.CreateClient(
        new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client)
    {
        var html = await client.GetStringAsync("/");
        return ReadAntiforgeryToken(html);
    }

    private static string ReadAntiforgeryToken(string html)
    {
        var match = AntiforgeryTokenPattern().Match(html);
        Assert.True(match.Success, "Ana sayfada anti-forgery token bulunamadı.");

        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private static MemoryStream CreateAssetWorkbook()
    {
        var stream = new MemoryStream();

        using (var workbook = new XLWorkbook())
        {
            var worksheet = workbook.Worksheets.Add("Varlık Tablosu");
            worksheet.Cell(1, 1).Value = "Tarih";
            worksheet.Cell(1, 2).Value = "Varlık Tutarı";
            worksheet.Cell(2, 1).Value = new DateTime(2021, 12, 1);
            worksheet.Cell(2, 2).Value = 1_000_000m;
            worksheet.Cell(3, 1).Value = new DateTime(2022, 1, 1);
            worksheet.Cell(3, 2).Value = 1_050_000m;
            workbook.SaveAs(stream);
        }

        stream.Position = 0;
        return stream;
    }

    private static MemoryStream CreateProducerPriceIndexWorkbook()
    {
        var stream = new MemoryStream();

        using (var workbook = new XLWorkbook())
        {
            var worksheet = workbook.Worksheets.Add("ÜFE Endeks Tablosu");
            string[] headers =
            [
                "Yıl", "Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran",
                "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık"
            ];

            for (var index = 0; index < headers.Length; index++)
            {
                worksheet.Cell(1, index + 1).Value = headers[index];
            }

            worksheet.Cell(2, 1).Value = 2021;
            worksheet.Cell(2, 13).Value = 100m;
            worksheet.Cell(3, 1).Value = 2022;
            worksheet.Cell(3, 2).Value = 125m;
            workbook.SaveAs(stream);
        }

        stream.Position = 0;
        return stream;
    }

    private static async Task<HttpResponseMessage> UploadWorkbookAsync(
        HttpClient client,
        string requestUri,
        string fileName,
        Stream stream,
        string antiforgeryToken)
    {
        using var content = new MultipartFormDataContent
        {
            { new StringContent(antiforgeryToken), "__RequestVerificationToken" }
        };
        using var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(fileContent, "file", fileName);

        return await client.PostAsync(requestUri, content);
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryTokenPattern();

    private sealed record AssetUploadResponse(
        bool IsValid,
        int ParsedCount,
        AssetUploadError[] Errors);

    private sealed record AssetUploadError(string Code, string Message);
}
