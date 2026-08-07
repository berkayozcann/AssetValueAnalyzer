using AssetValueAnalyzer.Application.Assets.Imports;
using Microsoft.AspNetCore.Mvc;

namespace AssetValueAnalyzer.Web.Controllers;

[Route("imports")]
public sealed class ImportsController(
    ReadAssetValuesService readAssetValuesService) : Controller
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
}
