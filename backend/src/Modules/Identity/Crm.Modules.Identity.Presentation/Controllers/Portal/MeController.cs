using Crm.BuildingBlocks.Presentation;
using Crm.Modules.Identity.Application;
using Crm.Modules.Identity.Application.Commands.ChangePortalPassword;
using Crm.Modules.Identity.Application.Commands.UpdatePortalProfile;
using Crm.Modules.Identity.Application.Queries.GetPortalMe;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Modules.Identity.Presentation.Controllers.Portal;

/// <summary>The signed-in portal customer: profile, language, password (Story 47).</summary>
[Area(CrmAreas.Portal)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class MeController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<PortalMeDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PortalMeDto>> Get(CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new GetPortalMeQuery(), cancellationToken));

    [HttpPut]
    [ProducesResponseType<PortalMeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PortalMeDto>> Update([FromBody] PortalProfileRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new UpdatePortalProfileCommand(request.DisplayName, request.PreferredLocale), cancellationToken));

    [HttpPost("change-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> ChangePassword(
        [FromBody] PortalChangePasswordRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new ChangePortalPasswordCommand(request.CurrentPassword, request.NewPassword), cancellationToken));
}

public sealed record PortalProfileRequest(string DisplayName, string PreferredLocale);

public sealed record PortalChangePasswordRequest(string CurrentPassword, string NewPassword);
