using AssetValueAnalyzer.Application.Assets.Imports;
using AssetValueAnalyzer.Application.ProducerPriceIndices.Imports;
using AssetValueAnalyzer.Web.Features.Reports;
using Microsoft.AspNetCore.Mvc;

namespace AssetValueAnalyzer.Web.Controllers;

[Route("imports")]
public sealed class ImportsController(
    ReadAssetValuesService readAssetValuesService,
    ReadProducerPriceIndicesService readProducerPriceIndicesService,
    IReportWorkspaceSession reportWorkspaceSession) : Controller
{
    private const long MultipartRequestLimit =
        ReadAssetValuesService.MaxFileSize + (64 * 1024);

    [HttpPost("assets/validate")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MultipartRequestLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = MultipartRequestLimit)]
    public async Task<IActionResult> UploadAssets(
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (reportWorkspaceSession.Get().CompletedReport is not null)
        {
            return Conflict(AssetFileValidationResult.Invalid(
                new AssetImportValidationError(
                    "CompletedReportLocked",
                    "Yeni bir dosya yüklemek için önce yeni rapor oluşturma akışını başlatın.")));
        }

        if (file is null)
        {
            return UnprocessableEntity(AssetFileValidationResult.Invalid(
                new AssetImportValidationError(
                    "MissingFile",
                    "Lütfen bir varlık dosyası seçin.")));
        }

        await using var stream = file.OpenReadStream();
        var parseResult = await readAssetValuesService.ReadAsync(
            stream,
            file.FileName,
            file.Length,
            cancellationToken);
        var response = AssetFileValidationResult.FromParseResult(parseResult);

        if (!response.IsValid)
        {
            reportWorkspaceSession.ClearAssetValues();
            return UnprocessableEntity(response);
        }

        reportWorkspaceSession.SaveAssetValues(file.FileName, parseResult.Values);
        return Ok(response);
    }

    [HttpPost("indices/validate")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MultipartRequestLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = MultipartRequestLimit)]
    public async Task<IActionResult> UploadProducerPriceIndices(
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (reportWorkspaceSession.Get().CompletedReport is not null)
        {
            return Conflict(ProducerPriceIndexFileValidationResult.Invalid(
                new ProducerPriceIndexImportValidationError(
                    "CompletedReportLocked",
                    "Yeni bir dosya yüklemek için önce yeni rapor oluşturma akışını başlatın.")));
        }

        if (file is null)
        {
            return UnprocessableEntity(ProducerPriceIndexFileValidationResult.Invalid(
                new ProducerPriceIndexImportValidationError(
                    "MissingFile",
                    "Lütfen bir endeks dosyası seçin.")));
        }

        await using var stream = file.OpenReadStream();
        var parseResult = await readProducerPriceIndicesService.ReadAsync(
            stream,
            file.FileName,
            file.Length,
            cancellationToken);
        var response = ProducerPriceIndexFileValidationResult.FromParseResult(parseResult);

        if (!response.IsValid)
        {
            reportWorkspaceSession.ClearProducerPriceIndices();
            return UnprocessableEntity(response);
        }

        reportWorkspaceSession.SaveProducerPriceIndices(file.FileName, parseResult.Values);
        return Ok(response);
    }

    [HttpPost("assets/clear")]
    [ValidateAntiForgeryToken]
    public IActionResult ClearAssets()
    {
        if (reportWorkspaceSession.Get().CompletedReport is not null)
        {
            return Conflict();
        }

        reportWorkspaceSession.ClearAssetValues();
        return NoContent();
    }

    [HttpPost("indices/clear")]
    [ValidateAntiForgeryToken]
    public IActionResult ClearProducerPriceIndices()
    {
        if (reportWorkspaceSession.Get().CompletedReport is not null)
        {
            return Conflict();
        }

        reportWorkspaceSession.ClearProducerPriceIndices();
        return NoContent();
    }
}
