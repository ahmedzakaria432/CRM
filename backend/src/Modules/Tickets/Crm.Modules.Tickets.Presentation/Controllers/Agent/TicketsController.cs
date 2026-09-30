using Crm.BuildingBlocks.Application.Paging;
using Crm.BuildingBlocks.Presentation;
using Crm.Modules.Tickets.Application;
using Crm.Modules.Tickets.Application.Commands.AssignTicket;
using Crm.Modules.Tickets.Application.Commands.ChangeTicketCategory;
using Crm.Modules.Tickets.Application.Commands.ChangeTicketPriority;
using Crm.Modules.Tickets.Application.Commands.ClaimTicket;
using Crm.Modules.Tickets.Application.Commands.CreateTicket;
using Crm.Modules.Tickets.Application.Commands.TransferTicket;
using Crm.Modules.Tickets.Application.Commands.TransitionTicket;
using Crm.Modules.Tickets.Application.Commands.UnassignTicket;
using Crm.Modules.Tickets.Application.Queries.GetTicketById;
using Crm.Modules.Tickets.Application.Queries.GetTicketQueueCounts;
using Crm.Modules.Tickets.Application.Queries.SearchTickets;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Modules.Tickets.Presentation.Controllers.Agent;

/// <summary>The agent's ticket surface (Story 15). tickets.read / tickets.write / tickets.assign are declared on each
/// request; every ticket is organisation-scoped. RowVersion is base64 - send back what the last response returned.</summary>
[Area(CrmAreas.Agent)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class TicketsController : ApiControllerBase
{
    /// <summary>Queues and search. queue = mine, unassigned or all (default); status and priority may repeat.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<TicketListItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<TicketListItemDto>>> Search(
        [FromQuery] string? queue, [FromQuery] string? search, [FromQuery(Name = "status")] string[]? status,
        [FromQuery(Name = "priority")] string[]? priority, [FromQuery] Guid? categoryId, [FromQuery] Guid? assigneeId,
        [FromQuery] Guid? departmentId, [FromQuery] Guid? branchId, [FromQuery] Guid? customerId, [FromQuery] string? sort,
        [FromQuery] bool descending = true, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        ToActionResult(await Sender.Send(
            new SearchTicketsQuery(
                queue, search, status ?? [], priority ?? [], categoryId, assigneeId, departmentId, branchId, customerId, sort,
                descending, page, pageSize),
            cancellationToken));

    [HttpGet("queues")]
    [ProducesResponseType<TicketQueueCountsDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TicketQueueCountsDto>> Queues(CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new GetTicketQueueCountsQuery(), cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<TicketDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketDto>> Get(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new GetTicketByIdQuery(id), cancellationToken));

    [HttpPost]
    [ProducesResponseType<TicketDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TicketDto>> Create([FromBody] CreateTicketRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new CreateTicketCommand(
            request.CustomerId, request.ContactId, request.Subject, request.Description, request.Priority, request.CategoryId,
            request.DepartmentId), cancellationToken));

    /// <summary>Action is one of Open, AwaitCustomer, PutOnHold, Resolve, Close, Reopen, Cancel.</summary>
    [HttpPost("{id:guid}/transitions")]
    [ProducesResponseType<TicketDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TicketDto>> Transition(Guid id, [FromBody] TransitionTicketRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new TransitionTicketCommand(id, request.Action, request.RowVersion), cancellationToken));

    /// <summary>Takes no rowVersion: two simultaneous claims have one winner, the loser gets 409 Tickets.AlreadyClaimed.</summary>
    [HttpPost("{id:guid}/claim")]
    [ProducesResponseType<TicketDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TicketDto>> Claim(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new ClaimTicketCommand(id), cancellationToken));

    [HttpPost("{id:guid}/assign")]
    [ProducesResponseType<TicketDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TicketDto>> Assign(Guid id, [FromBody] AssignTicketRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new AssignTicketCommand(id, request.AgentId, request.RowVersion), cancellationToken));

    [HttpPost("{id:guid}/unassign")]
    [ProducesResponseType<TicketDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TicketDto>> Unassign(Guid id, [FromBody] TicketRowVersionRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new UnassignTicketCommand(id, request.RowVersion), cancellationToken));

    /// <summary>Moves the ticket to another active department of the tenant; clears the assignee and category.</summary>
    [HttpPost("{id:guid}/transfer")]
    [ProducesResponseType<TicketDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TicketDto>> Transfer(Guid id, [FromBody] TransferTicketRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new TransferTicketCommand(id, request.DepartmentId, request.RowVersion), cancellationToken));

    [HttpPut("{id:guid}/priority")]
    [ProducesResponseType<TicketDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TicketDto>> ChangePriority(
        Guid id, [FromBody] ChangeTicketPriorityRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new ChangeTicketPriorityCommand(id, request.Priority, request.RowVersion), cancellationToken));

    [HttpPut("{id:guid}/category")]
    [ProducesResponseType<TicketDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TicketDto>> ChangeCategory(
        Guid id, [FromBody] ChangeTicketCategoryRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new ChangeTicketCategoryCommand(id, request.CategoryId, request.RowVersion), cancellationToken));
}

public sealed record CreateTicketRequest(
    Guid CustomerId, string Subject, string Description, Guid? ContactId = null, string? Priority = null, Guid? CategoryId = null,
    Guid? DepartmentId = null);

public sealed record TransitionTicketRequest(string Action, byte[] RowVersion);

public sealed record AssignTicketRequest(Guid AgentId, byte[] RowVersion);

public sealed record TicketRowVersionRequest(byte[] RowVersion);

public sealed record TransferTicketRequest(Guid DepartmentId, byte[] RowVersion);

public sealed record ChangeTicketPriorityRequest(string Priority, byte[] RowVersion);

public sealed record ChangeTicketCategoryRequest(Guid CategoryId, byte[] RowVersion);
