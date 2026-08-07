using AssetValueAnalyzer.Application.Assets.Imports;
using AssetValueAnalyzer.Application.ProducerPriceIndices.Imports;
using Microsoft.AspNetCore.Mvc;

namespace AssetValueAnalyzer.Web.Controllers;

[Route("imports")]
public sealed class ImportsController(
    ReadAssetValuesService readAssetValuesService,
    ReadProducerPriceIndicesService readProducerPriceIndicesService) : Controller
{
    private const long MultipartRequestLimit =
        ReadAssetValuesService.MaxFileSize + (64 * 1024);

    [HttpPost("assets/validate")]
    [RequestSizeLimit(MultipartRequestLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = MultipartRequestLimit)]
    public async Task<IActionResult> UploadAssets(
        IFormFile? file,
        CancellationToken cancellationToken)
    {
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

        return response.IsValid
            ? Ok(response)
            : UnprocessableEntity(response);
    }

    [HttpPost("indices/validate")]
    [RequestSizeLimit(MultipartRequestLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = MultipartRequestLimit)]
    public async Task<IActionResult> UploadProducerPriceIndices(
        IFormFile? file,
        CancellationToken cancellationToken)
    {
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

        return response.IsValid
            ? Ok(response)
            : UnprocessableEntity(response);
    }
}
