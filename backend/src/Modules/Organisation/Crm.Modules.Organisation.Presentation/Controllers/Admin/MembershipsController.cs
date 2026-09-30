using Crm.BuildingBlocks.Presentation;
using Crm.Modules.Organisation.Application;
using Crm.Modules.Organisation.Application.Commands.AddMembership;
using Crm.Modules.Organisation.Application.Commands.ChangeMembershipRole;
using Crm.Modules.Organisation.Application.Commands.RemoveMembership;
using Crm.Modules.Organisation.Application.Queries.ListMemberships;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Modules.Organisation.Presentation.Controllers.Admin;

[Area(CrmAreas.Admin)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class MembershipsController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<MembershipDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MembershipDto>>> List(
        [FromQuery] Guid? userId, [FromQuery] Guid? departmentId, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new ListMembershipsQuery(userId, departmentId), cancellationToken));

    [HttpPost]
    [ProducesResponseType<MembershipDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MembershipDto>> Create(
        [FromBody] AddMembershipRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(
            new AddMembershipCommand(request.UserId, request.DepartmentId, request.Role), cancellationToken));

    [HttpPut("{id:guid}/role")]
    [ProducesResponseType<MembershipDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MembershipDto>> ChangeRole(
        Guid id, [FromBody] ChangeMembershipRoleRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new ChangeMembershipRoleCommand(id, request.Role, request.RowVersion), cancellationToken));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new RemoveMembershipCommand(id), cancellationToken));
}

public sealed record AddMembershipRequest(Guid UserId, Guid DepartmentId, string Role);

public sealed record ChangeMembershipRoleRequest(string Role, byte[] RowVersion);
