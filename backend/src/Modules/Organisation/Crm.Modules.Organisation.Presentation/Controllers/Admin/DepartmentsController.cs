using Crm.BuildingBlocks.Presentation;
using Crm.BuildingBlocks.Presentation.Authorization;
using Crm.Modules.Organisation.Application;
using Crm.Modules.Organisation.Application.Commands.CreateDepartment;
using Crm.Modules.Organisation.Application.Commands.DeactivateDepartment;
using Crm.Modules.Organisation.Application.Commands.RenameDepartment;
using Crm.Modules.Organisation.Application.Queries.ListDepartments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Modules.Organisation.Presentation.Controllers.Admin;

[Area(CrmAreas.Admin)]
[Authorize(Policy = CrmPolicies.Administrator)]
public sealed class DepartmentsController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<DepartmentDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DepartmentDto>>> List(
        [FromQuery] Guid? branchId, [FromQuery] bool activeOnly, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new ListDepartmentsQuery(branchId, activeOnly), cancellationToken));

    [HttpPost]
    [ProducesResponseType<DepartmentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<DepartmentDto>> Create(
        [FromBody] CreateDepartmentRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(
            new CreateDepartmentCommand(request.BranchId, request.Code, request.NameAr, request.NameEn), cancellationToken));

    [HttpPut("{id:guid}/name")]
    [ProducesResponseType<DepartmentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DepartmentDto>> Rename(
        Guid id, [FromBody] RenameRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new RenameDepartmentCommand(id, request.NameAr, request.NameEn, request.RowVersion), cancellationToken));

    [HttpPost("{id:guid}/deactivate")]
    [ProducesResponseType<DepartmentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<DepartmentDto>> Deactivate(
        Guid id, [FromBody] DeactivateRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new DeactivateDepartmentCommand(id, request.RowVersion), cancellationToken));
}

public sealed record CreateDepartmentRequest(Guid BranchId, string Code, string NameAr, string NameEn);
