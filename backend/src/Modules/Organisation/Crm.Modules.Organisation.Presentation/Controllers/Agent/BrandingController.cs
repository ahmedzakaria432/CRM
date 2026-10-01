using Crm.BuildingBlocks.Presentation;
using Crm.Modules.Organisation.Application;
using Crm.Modules.Organisation.Application.Queries.GetCurrentBranding;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Modules.Organisation.Presentation.Controllers.Agent;

/// <summary>Story 58: the signed-in staff user own tenant branding, for the agent and admin shell.</summary>
[Area(CrmAreas.Agent)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class BrandingController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<PublicBrandingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PublicBrandingDto>> Get(CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new GetCurrentBrandingQuery(), cancellationToken));
}
