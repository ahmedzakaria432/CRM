using Crm.BuildingBlocks.Presentation;
using Crm.Modules.Identity.Application;
using Crm.Modules.Identity.Application.Commands.ChangePassword;
using Crm.Modules.Identity.Application.Queries.GetMe;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Modules.Identity.Presentation.Controllers.Agent;

/// <summary>The signed-in user: profile, memberships, roles, and effective permissions (which follow the active
/// scope). change-password lives here, not under Auth/, so an expired token refreshes before it runs.</summary>
[Area(CrmAreas.Agent)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public sealed class MeController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<MeDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<MeDto>> Get(CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new GetMeQuery(), cancellationToken));

    [HttpPost("change-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new ChangePasswordCommand(request.CurrentPassword, request.NewPassword), cancellationToken));
}

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
