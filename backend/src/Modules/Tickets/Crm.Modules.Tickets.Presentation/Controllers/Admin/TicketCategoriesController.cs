using Crm.BuildingBlocks.Presentation;
using Crm.Modules.Tickets.Application;
using Crm.Modules.Tickets.Application.Commands.CreateCategory;
using Crm.Modules.Tickets.Application.Commands.DeactivateCategory;
using Crm.Modules.Tickets.Application.Commands.ReactivateCategory;
using Crm.Modules.Tickets.Application.Commands.UpdateCategory;
using Crm.Modules.Tickets.Application.Queries.GetCategory;
using Crm.Modules.Tickets.Application.Queries.ListCategories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Modules.Tickets.Presentation.Controllers.Admin;

/// <summary>Ticket category administration (Story 15; admin.organisation). Categories are never deleted, only deactivated.
/// RowVersion is base64 - send back what the last response returned.</summary>
[Area(CrmAreas.Admin)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class TicketCategoriesController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<TicketCategoryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TicketCategoryDto>>> List(
        [FromQuery] Guid? departmentId, [FromQuery] bool activeOnly = false, CancellationToken cancellationToken = default) =>
        ToActionResult(await Sender.Send(new ListCategoriesQuery(departmentId, activeOnly), cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<TicketCategoryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketCategoryDto>> Get(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new GetCategoryQuery(id), cancellationToken));

    [HttpPost]
    [ProducesResponseType<TicketCategoryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TicketCategoryDto>> Create([FromBody] CreateTicketCategoryRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new CreateCategoryCommand(
            request.DepartmentId, request.NameAr, request.NameEn, request.DisplayOrder, request.IsCustomerVisible), cancellationToken));

    [HttpPut("{id:guid}")]
    [ProducesResponseType<TicketCategoryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TicketCategoryDto>> Update(
        Guid id, [FromBody] UpdateTicketCategoryRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new UpdateCategoryCommand(
            id, request.NameAr, request.NameEn, request.DisplayOrder, request.IsCustomerVisible, request.RowVersion), cancellationToken));

    [HttpPost("{id:guid}/deactivate")]
    [ProducesResponseType<TicketCategoryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TicketCategoryDto>> Deactivate(
        Guid id, [FromBody] TicketCategoryRowVersionRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new DeactivateCategoryCommand(id, request.RowVersion), cancellationToken));

    [HttpPost("{id:guid}/reactivate")]
    [ProducesResponseType<TicketCategoryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TicketCategoryDto>> Reactivate(
        Guid id, [FromBody] TicketCategoryRowVersionRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new ReactivateCategoryCommand(id, request.RowVersion), cancellationToken));
}

public sealed record CreateTicketCategoryRequest(Guid DepartmentId, string NameAr, string NameEn, int DisplayOrder, bool IsCustomerVisible);

public sealed record UpdateTicketCategoryRequest(string NameAr, string NameEn, int DisplayOrder, bool IsCustomerVisible, byte[] RowVersion);

public sealed record TicketCategoryRowVersionRequest(byte[] RowVersion);
