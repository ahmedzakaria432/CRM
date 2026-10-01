using Crm.BuildingBlocks.Presentation;
using Crm.Modules.Organisation.Application;
using Crm.Modules.Organisation.Application.Commands.ChangeBranding;
using Crm.Modules.Organisation.Application.Commands.RevertBranding;
using Crm.Modules.Organisation.Application.Commands.UploadBrandLogo;
using Crm.Modules.Organisation.Application.Queries.GetBrandingSettings;
using Crm.Modules.Organisation.Application.Queries.PreviewBranding;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Modules.Organisation.Presentation.Controllers.Admin;

/// <summary>Story 58: admin.branding - settings, preview, logo upload, change, revert.</summary>
[Area(CrmAreas.Admin)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class BrandingController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<BrandingSettingsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BrandingSettingsDto>> Get(CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new GetBrandingSettingsQuery(), cancellationToken));

    [HttpGet("preview")]
    [ProducesResponseType<BrandingPreviewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BrandingPreviewDto>> Preview(
        [FromQuery] string primaryColour, [FromQuery] string accentColour, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new PreviewBrandingQuery(primaryColour, accentColour), cancellationToken));

    /// <summary>The first multipart endpoint: field "file". The request is capped at 1 MiB so a file between 512 KB and
    /// 1 MiB reaches the domain and gets the localised 422. The file name and declared content type are never read (S2).</summary>
    [HttpPost("logos")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(1_048_576)]
    [RequestFormLimits(MultipartBodyLengthLimit = 1_048_576)]
    [ProducesResponseType<BrandLogoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<BrandLogoDto>> UploadLogo(IFormFile file, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(file);

        using var stream = new MemoryStream();
        await file.CopyToAsync(stream, cancellationToken);
        return ToActionResult(await Sender.Send(new UploadBrandLogoCommand(stream.ToArray()), cancellationToken));
    }

    [HttpPut]
    [ProducesResponseType<BrandingSettingsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<BrandingSettingsDto>> Change(
        [FromBody] ChangeBrandingRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(
            new ChangeBrandingCommand(
                request.DisplayNameAr, request.DisplayNameEn, request.PrimaryColour, request.AccentColour,
                request.ArabicFont, request.LightLogoId, request.DarkLogoId, request.RowVersion),
            cancellationToken));

    [HttpPost("revert")]
    [ProducesResponseType<BrandingSettingsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<BrandingSettingsDto>> Revert(
        [FromBody] RevertBrandingRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new RevertBrandingCommand(request.RowVersion), cancellationToken));
}

public sealed record ChangeBrandingRequest(
    string DisplayNameAr,
    string DisplayNameEn,
    string PrimaryColour,
    string AccentColour,
    string ArabicFont,
    Guid? LightLogoId,
    Guid? DarkLogoId,
    byte[] RowVersion);

public sealed record RevertBrandingRequest(byte[] RowVersion);
