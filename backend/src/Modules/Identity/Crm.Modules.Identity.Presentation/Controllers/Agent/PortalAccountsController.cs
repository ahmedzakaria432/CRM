using Crm.BuildingBlocks.Presentation;
using Crm.Modules.Identity.Application;
using Crm.Modules.Identity.Application.Commands.ProvisionPortalAccount;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Modules.Identity.Presentation.Controllers.Agent;

/// <summary>An agent sets (or resets) the password of a portal-enabled contact (customers.write). Creates the portal
/// account on first use (Story 47, D19). The agent tells the customer the password out of band.</summary>
[Area(CrmAreas.Agent)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class PortalAccountsController : ApiControllerBase
{
    [HttpPost]
    [ProducesResponseType<PortalAccountDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PortalAccountDto>> Provision(
        [FromBody] ProvisionPortalAccountRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(
            new ProvisionPortalAccountCommand(request.CustomerId, request.ContactId, request.Password), cancellationToken));
}

public sealed record ProvisionPortalAccountRequest(Guid CustomerId, Guid ContactId, string Password);
