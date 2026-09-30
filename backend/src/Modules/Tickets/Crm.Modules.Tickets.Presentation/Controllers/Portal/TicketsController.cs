using Crm.BuildingBlocks.Application.Paging;
using Crm.BuildingBlocks.Presentation;
using Crm.Modules.Tickets.Application;
using Crm.Modules.Tickets.Application.Commands.ReopenPortalTicket;
using Crm.Modules.Tickets.Application.Commands.ReplyFromPortal;
using Crm.Modules.Tickets.Application.Commands.RequestTicketClosure;
using Crm.Modules.Tickets.Application.Commands.SubmitPortalTicket;
using Crm.Modules.Tickets.Application.Queries.GetPortalTicket;
using Crm.Modules.Tickets.Application.Queries.GetPortalTicketThread;
using Crm.Modules.Tickets.Application.Queries.ListPortalTickets;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Modules.Tickets.Presentation.Controllers.Portal;

/// <summary>The portal customer own tickets (Story 47, stories 48 and 49). Every action needs the portal JWT and
/// portal.tickets. Another customer ticket, an unknown id and (for a contact who is not the account admin) a colleague
/// ticket are all 404. Customers see customer-friendly statuses and public messages only.</summary>
[Area(CrmAreas.Portal)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class TicketsController : ApiControllerBase
{
    /// <summary>tab = open (default) or closed; status = Received, InProgress, AwaitingYourReply, Resolved or Closed;
    /// search matches number or subject. Newest activity first.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<PortalTicketSummaryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<PortalTicketSummaryDto>>> List(
        [FromQuery] string? tab, [FromQuery] string? status, [FromQuery] string? search, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) =>
        ToActionResult(await Sender.Send(new ListPortalTicketsQuery(tab, status, search, page, pageSize), cancellationToken));

    /// <summary>Raise a request: channel Portal, in the department that owns the customer.</summary>
    [HttpPost]
    [ProducesResponseType<PortalTicketDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PortalTicketDto>> Submit([FromBody] PortalSubmitTicketRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new SubmitPortalTicketCommand(request.Subject, request.Description, request.CategoryId), cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<PortalTicketDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PortalTicketDto>> Get(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new GetPortalTicketQuery(id), cancellationToken));

    /// <summary>The public messages, oldest first. Pass nextCursor as `after` for the next page.</summary>
    [HttpGet("{id:guid}/thread")]
    [ProducesResponseType<PortalThreadPageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PortalThreadPageDto>> Thread(
        Guid id, [FromQuery] int? after, [FromQuery] int limit = 50, CancellationToken cancellationToken = default) =>
        ToActionResult(await Sender.Send(new GetPortalTicketThreadQuery(id, after, limit), cancellationToken));

    /// <summary>Write on the ticket. A reply to a Resolved or awaiting-your-reply ticket reopens it.</summary>
    [HttpPost("{id:guid}/replies")]
    [ProducesResponseType<PortalTicketDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PortalTicketDto>> Reply(Guid id, [FromBody] PortalReplyRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new ReplyFromPortalCommand(id, request.Body), cancellationToken));

    /// <summary>My issue is solved: the ticket becomes Resolved.</summary>
    [HttpPost("{id:guid}/request-closure")]
    [ProducesResponseType<PortalTicketDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PortalTicketDto>> RequestClosure(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new RequestTicketClosureCommand(id), cancellationToken));

    /// <summary>Reopen a Resolved ticket. A Closed ticket is 422.</summary>
    [HttpPost("{id:guid}/reopen")]
    [ProducesResponseType<PortalTicketDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PortalTicketDto>> Reopen(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new ReopenPortalTicketCommand(id), cancellationToken));
}

public sealed record PortalSubmitTicketRequest(string Subject, string Description, Guid? CategoryId = null);

public sealed record PortalReplyRequest(string Body);
