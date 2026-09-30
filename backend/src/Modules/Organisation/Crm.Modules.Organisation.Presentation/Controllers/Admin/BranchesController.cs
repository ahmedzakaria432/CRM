using Crm.BuildingBlocks.Presentation;
using Crm.Modules.Organisation.Application;
using Crm.Modules.Organisation.Application.Commands.CreateBranch;
using Crm.Modules.Organisation.Application.Commands.DeactivateBranch;
using Crm.Modules.Organisation.Application.Commands.RenameBranch;
using Crm.Modules.Organisation.Application.Queries.ListBranches;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Modules.Organisation.Presentation.Controllers.Admin;

[Area(CrmAreas.Admin)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class BranchesController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<BranchDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BranchDto>>> List(
        [FromQuery] bool activeOnly, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new ListBranchesQuery(activeOnly), cancellationToken));

    [HttpPost]
    [ProducesResponseType<BranchDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BranchDto>> Create(
        [FromBody] CreateBranchRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new CreateBranchCommand(request.Code, request.NameAr, request.NameEn), cancellationToken));

    [HttpPut("{id:guid}/name")]
    [ProducesResponseType<BranchDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BranchDto>> Rename(
        Guid id, [FromBody] RenameRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new RenameBranchCommand(id, request.NameAr, request.NameEn, request.RowVersion), cancellationToken));

    [HttpPost("{id:guid}/deactivate")]
    [ProducesResponseType<BranchDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<BranchDto>> Deactivate(
        Guid id, [FromBody] DeactivateRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new DeactivateBranchCommand(id, request.RowVersion), cancellationToken));
}

public sealed record CreateBranchRequest(string Code, string NameAr, string NameEn);

/// <summary>RowVersion is base64 over the wire (byte[] serialises that way) - send back exactly what the GET
/// returned; re-encoding it produces a spurious 409.</summary>
public sealed record RenameRequest(string NameAr, string NameEn, byte[] RowVersion);

public sealed record DeactivateRequest(byte[] RowVersion);
