using Crm.BuildingBlocks.Presentation;
using Crm.Modules.Organisation.Application;
using Crm.Modules.Organisation.Application.Queries.GetMyScope;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Modules.Organisation.Presentation.Controllers.Agent;

[Area(CrmAreas.Agent)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public sealed class MyScopeController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<MyScopeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<MyScopeDto>> Get(CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new GetMyScopeQuery(), cancellationToken));
}
