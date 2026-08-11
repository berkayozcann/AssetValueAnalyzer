using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using AssetValueAnalyzer.Application.ExchangeRates.External;
using AssetValueAnalyzer.Application.ExchangeRates.Synchronization;
using AssetValueAnalyzer.IntegrationTests.Support;
using AssetValueAnalyzer.Infrastructure;
using AssetValueAnalyzer.Infrastructure.BackgroundJobs;
using AssetValueAnalyzer.Infrastructure.Reports.Exporting;
using AssetValueAnalyzer.Web.Features.ExchangeRates.Realtime;
using AssetValueAnalyzer.Web.Hosting;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AssetValueAnalyzer.IntegrationTests.Http;

public sealed partial class WebApplicationSmokeTests(
    AssetValueAnalyzerWebApplicationFactory factory)
    : IClassFixture<AssetValueAnalyzerWebApplicationFactory>
{
    [Fact]
    public void WebHost_UsesIsolatedTestingConfigurationWithoutRealWorkersOrFinmaksClient()
    {
        var environment = factory.Services.GetRequiredService<IHostEnvironment>();
        var configuration = factory.Services.GetRequiredService<IConfiguration>();
        var connectionString = configuration.GetConnectionString(
            DependencyInjection.DatabaseConnectionName);
        var hostedServices = factory.Services.GetServices<IHostedService>().ToArray();
        using var scope = factory.Services.CreateScope();
        var finmaksClient = scope.ServiceProvider
            .GetRequiredService<IFinmaksExchangeRateClient>();

        Assert.Equal(
            AssetValueAnalyzerWebApplicationFactory.TestingEnvironmentName,
            environment.EnvironmentName);
        Assert.Contains("integration-test.invalid", connectionString);
        Assert.DoesNotContain(
            hostedServices,
            service => service is ExchangeRateInitializationHostedService);
        Assert.DoesNotContain(
            hostedServices,
            service => service is ExchangeRateRecurringJobRegistrationHostedService);
        Assert.DoesNotContain(
            hostedServices,
            service => service.GetType().Namespace?.StartsWith("Hangfire", StringComparison.Ordinal) == true);
        Assert.Contains("BlockedFinmaksExchangeRateClient", finmaksClient.GetType().Name);
        Assert.Equal(1, factory.DatabaseStartup.ApplyMigrationsCallCount);
        Assert.Equal(0, factory.DatabaseStartup.EnsureReadyCallCount);
    }

    [Fact]
    public void WebHost_UsesSignalRNotifierInsteadOfInfrastructureFallback()
    {
        using var scope = factory.Services.CreateScope();

        var notifier = scope.ServiceProvider
            .GetRequiredService<IExchangeRateSynchronizationNotifier>();

        Assert.IsType<SignalRExchangeRateSynchronizationNotifier>(notifier);
    }

    [Fact]
    public async Task HealthEndpoint_WhenStartupCompleted_ReturnsHealthy()
    {
        using var client = CreateClient();

        var response = await client.GetAsync("/health");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", body);
    }

    [Fact]
    public async Task LivenessEndpoint_RemainsHealthyWhenReadinessIsUnhealthy()
    {
        using var baseFactory = new AssetValueAnalyzerWebApplicationFactory();
        using var unhealthyFactory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.ReplaceReadinessChecksWithUnhealthyProbe()));
        using var client = unhealthyFactory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        var readinessResponse = await client.GetAsync("/health");
        var livenessResponse = await client.GetAsync("/health/live");
        var body = await livenessResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, readinessResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, livenessResponse.StatusCode);
        Assert.Equal("Healthy", body);
    }

    [Fact]
    public async Task HomePage_UsesRealPipelineAndReturnsAntiforgeryToken()
    {
        using var client = CreateClient();

        var response = await client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Yeni Analiz Oluştur", html);
        Assert.NotEmpty(ReadAntiforgeryToken(html));
        Assert.Contains("/lib/signalr/signalr.min.js", html);
    }

    [Fact]
    public async Task HomePage_DeclaresFixedLightPalette()
    {
        using var client = CreateClient();

        var html = await client.GetStringAsync("/");
        var css = await client.GetStringAsync("/css/app.css");

        Assert.Contains("<meta name=\"color-scheme\" content=\"only light\"", html);
        Assert.Contains("color-scheme:light only", css);
        Assert.Contains("--color-canvas-950:#e7e5de", css);
        Assert.Contains("--color-surface-900:#f4f2ec", css);
        Assert.Contains("--color-surface-800:#fbfaf7", css);
        Assert.Contains("--color-brand-400:#245c63", css);
        Assert.Contains("--color-step-muted:#666e69", css);
        Assert.Contains(".md\\:sticky{position:sticky}", css);
        Assert.Contains("font-display:optional", css);
        Assert.Contains("rel=\"preload\" href=\"/fonts/inter-latin-wght-normal.woff2\"", html);
        Assert.Contains("rel=\"preload\" href=\"/fonts/inter-latin-ext-wght-normal.woff2\"", html);
        Assert.Contains("rel=\"preload\" href=\"/fonts/manrope-latin-wght-normal.woff2\"", html);
        Assert.Contains("rel=\"preload\" href=\"/fonts/manrope-latin-ext-wght-normal.woff2\"", html);
    }

    [Fact]
    public async Task SharedStyles_AnimateAppNavigationAndPageIntro()
    {
        using var client = CreateClient();

        var html = await client.GetStringAsync("/");
        var css = await client.GetStringAsync("/css/app.css");
        var javascript = await client.GetStringAsync("/js/app.js");

        Assert.DoesNotContain("@view-transition", css);
        Assert.DoesNotContain("page-fallback-enter", css);
        Assert.DoesNotContain("data-wizard-ready=false", css);
        Assert.DoesNotContain("pageTransition", html);
        Assert.Contains("AssetValueAnalyzer.Navigation.Pending", html);
        Assert.Contains("window.sessionStorage.removeItem(\"AssetValueAnalyzer.Navigation.Pending\")", html);
        Assert.Contains("[data-app-navigation=entering] [data-page-body-motion]", css);
        Assert.Contains("[data-app-navigation=leaving] main", css);
        Assert.Contains("app-page-enter", css);
        Assert.Contains("app-page-intro-enter", css);
        Assert.Contains("app-rate-card-enter", css);
        Assert.Contains("[data-page-intro]", css);
        Assert.Contains("data-page-intro", html);
        Assert.Contains("data-page-body-motion", html);
        Assert.Contains("data-page-rate-card", html);
        Assert.Contains("const beginAppNavigation = (navigate) =>", javascript);
        Assert.Contains("prefers-reduced-motion: reduce", javascript);
        Assert.Contains("window.location.assign(url.href)", javascript);
        Assert.Contains("document.addEventListener(\"submit\"", javascript);
        Assert.Contains("anchor.matches(\"[data-report-download]\")", javascript);
    }

    [Fact]
    public async Task HomePage_RendersWizardStateFromServerBeforeJavaScriptRuns()
    {
        using var client = CreateClient();

        var html = await client.GetStringAsync("/");
        var javascript = await client.GetStringAsync("/js/app.js");

        Assert.Contains("<html lang=\"tr\" data-report-wizard-step=\"1\">", html);
        Assert.Contains("data-wizard-ready=\"true\"", html);
        Assert.Contains("data-initial-step=\"1\"", html);
        Assert.DoesNotMatch("data-step-panel=\"1\"[^>]*hidden", html);
        Assert.Matches("data-step-panel=\"2\"[^>]*hidden", html);
        Assert.Matches("data-step-panel=\"3\"[^>]*hidden", html);
        Assert.DoesNotContain("AssetValueAnalyzer.ReportWizard.", html);
        Assert.DoesNotContain("data-confirmation-summary-ready", html);
        Assert.DoesNotContain("data-month-selection-ready", html);
        Assert.StartsWith("(() => {", javascript);
        Assert.DoesNotContain("DOMContentLoaded", javascript);
        Assert.Contains("const serverRenderedStep = Number(wizard.dataset.initialStep);", javascript);
        Assert.Contains("state.step = requestedInitialStep;", javascript);
        Assert.Contains("const persistWizardState = () =>", javascript);
        Assert.Contains("let pendingWizardStateBody = null;", javascript);
        Assert.Contains("while (pendingWizardStateBody)", javascript);
        Assert.Contains("pendingWizardStateBody = body;", javascript);
        Assert.Contains("keepalive: true", javascript);
        Assert.Contains("wizard.dataset.wizardReady = \"true\";", javascript);
        Assert.DoesNotContain("wizardStorageKeys", javascript);
        Assert.DoesNotContain("restoreMonthSelection", javascript);
        Assert.Matches("""if \(state\.step === 3\) \{\s+updateConfirmationSummary\(\);\s+\}\s+renderStep\(\);""", javascript);
    }

    [Fact]
    public async Task HomePage_KeepsRangeContinueButtonStableWhileRestoredRangeIsValidated()
    {
        using var client = CreateClient();

        var javascript = await client.GetStringAsync("/js/app.js");

        Assert.Contains("rangeContinueButton.disabled = isInvalid;", javascript);
        Assert.Contains("rangeContinueButton.setAttribute(\"aria-disabled\", String(isInvalid));", javascript);
        Assert.Contains("rangeContinueButton.setAttribute(\"aria-busy\", String(isValidating));", javascript);
        Assert.Contains("continueButton.disabled = state.reportLocked || !(", javascript);
        Assert.Contains("state.rangeValidationState !== \"validating\"", javascript);
        Assert.Matches(
            """const validateWizardRange = async \(\) => \{\s+const requestId = \+\+state\.rangeValidationRequestId;\s+const localError""",
            javascript);
    }

    [Fact]
    public async Task HomePage_UsesDistinctAccessibleNamesForUploadAndMonthControls()
    {
        using var client = CreateClient();

        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/"));
        var javascript = await client.GetStringAsync("/js/app.js");

        Assert.Contains("aria-label=\"Aylık Varlık Verisi dosyası seç\"", html);
        Assert.Contains("aria-label=\"Yİ-ÜFE Endeks Verisi dosyası seç\"", html);
        Assert.Contains("aria-label=\"Başlangıç ayı: Seçilmedi\"", html);
        Assert.Contains("aria-label=\"Bitiş ayı: Seçilmedi\"", html);
        Assert.Contains("updateMonthPickerAccessibleName", javascript);
        Assert.True(
            javascript.Split("updateMonthPickerAccessibleName(picker)").Length - 1 >= 4,
            "Ay seçici erişilebilir adı; seçim, temizleme, ilk yükleme ve sıfırlamada güncellenmelidir.");
        Assert.Matches(
            """if \(event\.key === "Escape"\) \{\s+hideInfoTooltips\(\);\s+closeMonthPickers\(\);""",
            javascript);
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
        Assert.Contains("data-rate-state=\"empty\"", html);
        Assert.Contains("data-refresh-url=\"/exchange-rates/card\"", html);
        Assert.Contains("USD / TRY", decodedHtml);
        Assert.Contains("Veri yok", decodedHtml);
        Assert.Contains("Kur verisi henüz alınmadı.", decodedHtml);
        Assert.Equal(1, html.Split("data-exchange-rate-card").Length - 1);
        Assert.DoesNotContain("<!DOCTYPE html>", html);
    }

    [Fact]
    public async Task ExchangeRateCard_ClientCatchesUpAfterBackgroundTabResumes()
    {
        using var client = CreateClient();

        var javascript = await client.GetStringAsync("/js/app.js");

        Assert.Contains("connection.on(\"exchangeRatesSynchronized\"", javascript);
        Assert.Contains("connection.onreconnected", javascript);
        Assert.Contains("connection.onclose", javascript);
        Assert.Contains("document.addEventListener(\"visibilitychange\"", javascript);
        Assert.Contains("window.addEventListener(\"focus\"", javascript);
        Assert.Contains("window.addEventListener(\"pageshow\"", javascript);
        Assert.Contains("catchUpExchangeRateCard", javascript);
        Assert.Contains("cardRefreshIntervalMilliseconds = 30_000", javascript);
        Assert.Contains("window.setInterval", javascript);
        Assert.Contains("window.clearInterval(refreshIntervalId)", javascript);
        Assert.Contains("isPageUnloading = false;", javascript);
        Assert.Contains("void refreshExchangeRateCard();", javascript);
        Assert.Contains("void startConnection();", javascript);
    }

    [Fact]
    public async Task ReportPage_WithoutFiles_RendersEmptyFinancialImpactReportState()
    {
        using var client = CreateClient();

        var response = await client.GetAsync("/reports");
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Finansal Etki Raporu", html);
        Assert.Contains("Henüz rapor oluşturulmadı", html);
        Assert.Contains("Yeni Analiz Oluştur", html);
        Assert.DoesNotContain("data-ready-file-count", html);
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
        var reportHtml = WebUtility.HtmlDecode(await client.GetStringAsync("/reports"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.True(result.IsValid);
        Assert.Equal(2, result.ParsedCount);
        Assert.Contains("smoke-assets.xlsx", homeHtml);
        Assert.Contains("data-ready-file-count=\"1\"", reportHtml);
        Assert.Contains("draft-file-list", reportHtml);
        Assert.Contains("report-info-grid", reportHtml);
        Assert.Contains("smoke-assets.xlsx", reportHtml);
        Assert.Contains("Doğrulandı", reportHtml);
        Assert.Contains("text-negative-400", reportHtml);
        Assert.Contains("Eksik", reportHtml);
        Assert.True(
            reportHtml.IndexOf("Aylık Varlık Verisi", StringComparison.Ordinal) <
            reportHtml.IndexOf("Yİ-ÜFE Endeks Verisi", StringComparison.Ordinal));
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
        using var dateStepContent = new FormUrlEncodedContent(
        [
            new("__RequestVerificationToken", token),
            new("Step", "2"),
            new("StartMonth", "2021-12"),
            new("EndMonth", "2022-01")
        ]);
        using var dateStepResponse = await client.PostAsync(
            "/reports/wizard-state",
            dateStepContent);
        var dateStepHtml = WebUtility.HtmlDecode(await client.GetStringAsync("/"));
        using var confirmationStepContent = new FormUrlEncodedContent(
        [
            new("__RequestVerificationToken", token),
            new("Step", "3"),
            new("StartMonth", "2021-12"),
            new("EndMonth", "2022-01")
        ]);
        using var confirmationStepResponse = await client.PostAsync(
            "/reports/wizard-state",
            confirmationStepContent);
        var confirmationStepHtml = WebUtility.HtmlDecode(await client.GetStringAsync("/"));

        var createResponse = await client.PostAsync("/reports/create", createContent);
        var reportResponse = await client.GetAsync("/reports");
        var reportHtml = await reportResponse.Content.ReadAsStringAsync();
        var decodedReportHtml = WebUtility.HtmlDecode(reportHtml);
        var completedHomeHtml = WebUtility.HtmlDecode(await client.GetStringAsync("/"));
        var downloadResponse = await client.GetAsync("/reports/download");
        await using var downloadedStream = await downloadResponse.Content.ReadAsStreamAsync();
        using var downloadedWorkbook = new XLWorkbook(downloadedStream);
        var downloadedWorksheet = downloadedWorkbook.Worksheet("Finansal Etki Raporu");

        Assert.Equal(HttpStatusCode.OK, assetResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, indexResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, dateStepResponse.StatusCode);
        Assert.Contains("<html lang=\"tr\" data-report-wizard-step=\"2\">", dateStepHtml);
        Assert.Contains("data-initial-step=\"2\"", dateStepHtml);
        Assert.Matches("""data-step-panel="1"[^>]*hidden""", dateStepHtml);
        Assert.DoesNotMatch("""data-step-panel="2"[^>]*hidden""", dateStepHtml);
        Assert.Matches("""data-step-panel="3"[^>]*hidden""", dateStepHtml);
        Assert.Contains("aria-label=\"Başlangıç ayı: Aralık 2021\"", dateStepHtml);
        Assert.Contains("aria-label=\"Bitiş ayı: Ocak 2022\"", dateStepHtml);
        Assert.DoesNotMatch("""data-step-two-continue[^>]*disabled""", dateStepHtml);
        Assert.Equal(HttpStatusCode.OK, confirmationStepResponse.StatusCode);
        Assert.Contains("<html lang=\"tr\" data-report-wizard-step=\"3\">", confirmationStepHtml);
        Assert.Contains("data-initial-step=\"3\"", confirmationStepHtml);
        Assert.Matches("""data-step-panel="1"[^>]*hidden""", confirmationStepHtml);
        Assert.Matches("""data-step-panel="2"[^>]*hidden""", confirmationStepHtml);
        Assert.DoesNotMatch("""data-step-panel="3"[^>]*hidden""", confirmationStepHtml);
        Assert.Contains("Aralık 2021 – Ocak 2022", confirmationStepHtml);
        Assert.Contains("data-summary-duration>2 ay", confirmationStepHtml);
        Assert.DoesNotContain("Rapor dönemi dosyanın ilk ve son ayına göre belirlendi.", confirmationStepHtml);
        Assert.Equal(HttpStatusCode.Redirect, createResponse.StatusCode);
        Assert.Equal("/reports", createResponse.Headers.Location?.OriginalString);
        Assert.Equal(HttpStatusCode.OK, reportResponse.StatusCode);
        Assert.Contains("Finansal Etki Raporu", decodedReportHtml);
        Assert.Contains("Aralık 2021 – Ocak 2022", decodedReportHtml);
        Assert.Contains("₺2.000.000,00", decodedReportHtml);
        Assert.Contains("aria-label=\"Başlangıç ayı: Aralık 2021\"", decodedReportHtml);
        Assert.Contains("aria-label=\"Bitiş ayı: Ocak 2022\"", decodedReportHtml);
        Assert.Contains("class=\"primary-button inline-flex h-10", reportHtml);
        Assert.Contains("data-new-analysis-action", reportHtml);
        Assert.Contains("data-report-download", reportHtml);
        Assert.Contains("href=\"/reports/download\"", reportHtml);
        Assert.Contains("sm:mr-auto", reportHtml);
        Assert.Contains("Finansal etki raporunuz hazır.", completedHomeHtml);
        Assert.Contains("class=\"primary-button inline-flex h-11", completedHomeHtml);
        Assert.Contains("data-new-analysis-action", completedHomeHtml);
        Assert.Matches("""data-step-one-continue[^>]*disabled""", completedHomeHtml);
        Assert.DoesNotContain("disabled=\"False\"", completedHomeHtml);
        Assert.Equal(2, reportHtml.Split("data-report-row").Length - 1);
        Assert.Contains("border-brand-400 bg-brand-500 text-paper-100", reportHtml);
        Assert.Contains("aria-current=\"page\"", reportHtml);
        Assert.Contains("data-current-page=\"1\"", reportHtml);
        Assert.Matches("""data-page-previous\s+disabled""", reportHtml);
        Assert.Matches("""data-page-next\s+disabled""", reportHtml);
        Assert.Contains("window.history.replaceState", await client.GetStringAsync("/js/app.js"));
        Assert.Contains("md:sticky md:left-40 md:z-30", reportHtml);
        Assert.DoesNotContain("class=\"sticky left-40", reportHtml);
        Assert.Equal(HttpStatusCode.OK, downloadResponse.StatusCode);
        Assert.Equal(
            XlsxFinancialImpactReportExporter.XlsxContentType,
            downloadResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal(
            "attachment",
            downloadResponse.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Contains(
            "finansal-etki-raporu-2021-12-2022-01.xlsx",
            downloadResponse.Content.Headers.ContentDisposition?.ToString());
        Assert.Equal(14, downloadedWorksheet.Row(4).CellsUsed().Count());
        Assert.Equal(new DateTime(2021, 12, 1), downloadedWorksheet.Cell(5, 1).GetDateTime());
        Assert.Equal(1_000_000m, downloadedWorksheet.Cell(5, 2).GetValue<decimal>());
        Assert.Equal(XLDataType.Number, downloadedWorksheet.Cell(5, 3).DataType);
        Assert.Equal(0m, downloadedWorksheet.Cell(5, 3).GetValue<decimal>());
        Assert.Equal(new DateTime(2022, 1, 1), downloadedWorksheet.Cell(6, 1).GetDateTime());
        Assert.Equal(1_050_000m, downloadedWorksheet.Cell(6, 2).GetValue<decimal>());

        var describedTooltipIds = TooltipDescriptionPattern()
            .Matches(reportHtml)
            .Select(match => match.Groups[1].Value)
            .ToArray();

        Assert.NotEmpty(describedTooltipIds);
        Assert.Equal(describedTooltipIds.Length, describedTooltipIds.Distinct().Count());
        Assert.All(describedTooltipIds, id => Assert.Contains($"id=\"{id}\"", reportHtml));
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

    [GeneratedRegex("aria-describedby=\"(info-tooltip-[a-f0-9]{32})\"")]
    private static partial Regex TooltipDescriptionPattern();

    private sealed record AssetUploadResponse(
        bool IsValid,
        int ParsedCount,
        AssetUploadError[] Errors);

    private sealed record AssetUploadError(string Code, string Message);
}
