using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using AssetValueAnalyzer.IntegrationTests.Support;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AssetValueAnalyzer.IntegrationTests.Http;

public sealed partial class DownloadableSampleFlowTests(
    AssetValueAnalyzerWebApplicationFactory factory)
    : IClassFixture<AssetValueAnalyzerWebApplicationFactory>
{
    [Fact]
    public async Task DownloadableSamples_WithAutomaticRange_ReturnValidCoveredPeriod()
    {
        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false,
                HandleCookies = true
            });
        var antiforgeryToken = await GetAntiforgeryTokenAsync(client);
        await using var assetStream = File.OpenRead(GetSamplePath("asset-values.xlsx"));
        await using var indexStream = File.OpenRead(GetSamplePath("producer-price-indices.xlsx"));

        using var assetResponse = await UploadSampleAsync(
            client,
            "/imports/assets/validate",
            "asset-values.xlsx",
            assetStream,
            antiforgeryToken);
        using var indexResponse = await UploadSampleAsync(
            client,
            "/imports/indices/validate",
            "producer-price-indices.xlsx",
            indexStream,
            antiforgeryToken);
        using var rangeContent = new FormUrlEncodedContent(
        [
            new("__RequestVerificationToken", antiforgeryToken),
            new("StartMonth", string.Empty),
            new("EndMonth", string.Empty)
        ]);

        using var rangeResponse = await client.PostAsync(
            "/reports/validate-range",
            rangeContent);
        using var rangeJson = JsonDocument.Parse(
            await rangeResponse.Content.ReadAsStringAsync());
        var root = rangeJson.RootElement;

        Assert.Equal(HttpStatusCode.OK, assetResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, indexResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, rangeResponse.StatusCode);
        Assert.True(root.GetProperty("isValid").GetBoolean());
        Assert.Equal("2021-12", root.GetProperty("effectiveStartMonth").GetString());
        Assert.Equal("2023-08", root.GetProperty("effectiveEndMonth").GetString());
        Assert.Equal(21, root.GetProperty("includedMonthCount").GetInt32());
        Assert.Empty(root.GetProperty("errors").EnumerateArray());
    }

    private static string GetSamplePath(string fileName) =>
        Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "../../../../..",
            "src/AssetValueAnalyzer.Web/wwwroot/samples",
            fileName));

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client)
    {
        var html = await client.GetStringAsync("/");
        var match = AntiforgeryTokenPattern().Match(html);
        Assert.True(match.Success, "Ana sayfada anti-forgery token bulunamadı.");

        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private static async Task<HttpResponseMessage> UploadSampleAsync(
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
}
