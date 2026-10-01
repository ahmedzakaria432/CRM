using Crm.BuildingBlocks.Presentation;
using Crm.Modules.Organisation.Application;
using Crm.Modules.Organisation.Application.Queries.GetBrandLogo;
using Crm.Modules.Organisation.Application.Queries.GetPublicBranding;
using Crm.SharedKernel.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Modules.Organisation.Presentation.Controllers.Public;

/// <summary>Story 58: public branding for the portal sign-in pages. Public Area: anonymous and rate limited per IP by
/// the global limiter (AREAS.md) - no [AllowAnonymous] needed or allowed.</summary>
[Area(CrmAreas.Public)]
public sealed class BrandingController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<PublicBrandingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PublicBrandingDto>> Get([FromQuery] string organisation, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new GetPublicBrandingQuery(organisation), cancellationToken));

    [HttpGet("logos/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Logo(Guid id, CancellationToken cancellationToken)
    {
        Result<BrandLogoContentDto> result = await Sender.Send(new GetBrandLogoQuery(id), cancellationToken);
        if (result.IsFailure)
        {
            return Problem(result.Error);
        }

        // The id is random and the content never changes for an id, so the response is immutable (D12). The headers
        // keep an uploaded image from ever running as a document (S2).
        Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.ContentSecurityPolicy = "default-src 'none'; style-src 'unsafe-inline'; sandbox";
        return File(result.Value.Content, result.Value.ContentType);
    }
}
