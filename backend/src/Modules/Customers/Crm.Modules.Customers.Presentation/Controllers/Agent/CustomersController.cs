using Crm.BuildingBlocks.Application.Paging;
using Crm.BuildingBlocks.Presentation;
using Crm.Modules.Customers.Application;
using Crm.Modules.Customers.Application.Commands.AddContact;
using Crm.Modules.Customers.Application.Commands.AddNote;
using Crm.Modules.Customers.Application.Commands.CreateCustomer;
using Crm.Modules.Customers.Application.Commands.DeactivateCustomer;
using Crm.Modules.Customers.Application.Commands.PinNote;
using Crm.Modules.Customers.Application.Commands.ReactivateCustomer;
using Crm.Modules.Customers.Application.Commands.RemoveContact;
using Crm.Modules.Customers.Application.Commands.RemoveNote;
using Crm.Modules.Customers.Application.Commands.SetPrimaryContact;
using Crm.Modules.Customers.Application.Commands.UpdateContact;
using Crm.Modules.Customers.Application.Commands.UpdateCustomer;
using Crm.Modules.Customers.Application.Queries.GetCustomerById;
using Crm.Modules.Customers.Application.Queries.LookupCustomers;
using Crm.Modules.Customers.Application.Queries.SearchCustomers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Modules.Customers.Presentation.Controllers.Agent;

/// <summary>The agent's customer surface (Story 11). customers.read for reads, customers.write for writes (declared
/// on each request); every row is organisation-scoped. RowVersion is base64 over the wire - send back exactly what
/// the last response returned.</summary>
[Area(CrmAreas.Agent)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class CustomersController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<CustomerListItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<CustomerListItemDto>>> Search(
        [FromQuery] string? search, [FromQuery] string? status, [FromQuery] string? type, [FromQuery] Guid? departmentId,
        [FromQuery] Guid? branchId, [FromQuery] string? sort, [FromQuery] bool descending = true, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) =>
        ToActionResult(await Sender.Send(
            new SearchCustomersQuery(search, status, type, departmentId, branchId, sort, descending, page, pageSize), cancellationToken));

    [HttpGet("lookup")]
    [ProducesResponseType<IReadOnlyList<CustomerLookupDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<CustomerLookupDto>>> Lookup(
        [FromQuery] string? search, [FromQuery] int take = 10, CancellationToken cancellationToken = default) =>
        ToActionResult(await Sender.Send(new LookupCustomersQuery(search, take), cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<CustomerDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerDto>> Get(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new GetCustomerByIdQuery(id), cancellationToken));

    /// <summary>200 with created = false and duplicateCandidates when an in-scope customer already has one of the
    /// contact emails/phones; re-send with confirmDuplicate = true to create anyway.</summary>
    [HttpPost]
    [ProducesResponseType<CreateCustomerOutcome>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CreateCustomerOutcome>> Create([FromBody] CreateCustomerRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new CreateCustomerCommand(
            request.DisplayNameAr, request.DisplayNameEn, request.Type, request.LegalName, request.PreferredLanguage,
            [.. (request.Contacts ?? []).Select(c => new NewContact(c.Name, c.JobTitle, c.Email, c.Phone, c.IsPrimary, c.HasPortalAccess))],
            request.ConfirmDuplicate, request.DepartmentId), cancellationToken));

    [HttpPut("{id:guid}")]
    [ProducesResponseType<CustomerDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CustomerDto>> Update(Guid id, [FromBody] UpdateCustomerRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new UpdateCustomerCommand(
            id, request.DisplayNameAr, request.DisplayNameEn, request.Type, request.LegalName, request.PreferredLanguage, request.RowVersion),
            cancellationToken));

    [HttpPost("{id:guid}/deactivate")]
    [ProducesResponseType<CustomerDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CustomerDto>> Deactivate(Guid id, [FromBody] CustomerRowVersionRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new DeactivateCustomerCommand(id, request.RowVersion), cancellationToken));

    [HttpPost("{id:guid}/reactivate")]
    [ProducesResponseType<CustomerDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CustomerDto>> Reactivate(Guid id, [FromBody] CustomerRowVersionRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new ReactivateCustomerCommand(id, request.RowVersion), cancellationToken));

    [HttpPost("{id:guid}/contacts")]
    [ProducesResponseType<CustomerDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CustomerDto>> AddContact(Guid id, [FromBody] AddContactRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new AddContactCommand(
            id, request.Name, request.JobTitle, request.Email, request.Phone, request.IsPrimary, request.HasPortalAccess, request.RowVersion),
            cancellationToken));

    [HttpPut("{id:guid}/contacts/{contactId:guid}")]
    [ProducesResponseType<CustomerDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CustomerDto>> UpdateContact(
        Guid id, Guid contactId, [FromBody] UpdateContactRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new UpdateContactCommand(
            id, contactId, request.Name, request.JobTitle, request.Email, request.Phone, request.HasPortalAccess, request.RowVersion),
            cancellationToken));

    [HttpPost("{id:guid}/contacts/{contactId:guid}/primary")]
    [ProducesResponseType<CustomerDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CustomerDto>> SetPrimaryContact(
        Guid id, Guid contactId, [FromBody] CustomerRowVersionRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new SetPrimaryContactCommand(id, contactId, request.RowVersion), cancellationToken));

    /// <summary>A POST with the rowversion body (like /deactivate), not a DELETE with a body.</summary>
    [HttpPost("{id:guid}/contacts/{contactId:guid}/remove")]
    [ProducesResponseType<CustomerDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CustomerDto>> RemoveContact(
        Guid id, Guid contactId, [FromBody] CustomerRowVersionRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new RemoveContactCommand(id, contactId, request.RowVersion), cancellationToken));

    [HttpPost("{id:guid}/notes")]
    [ProducesResponseType<CustomerDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CustomerDto>> AddNote(Guid id, [FromBody] AddNoteRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new AddNoteCommand(id, request.Body), cancellationToken));

    [HttpPut("{id:guid}/notes/{noteId:guid}/pin")]
    [ProducesResponseType<CustomerDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerDto>> PinNote(
        Guid id, Guid noteId, [FromBody] PinNoteRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new PinNoteCommand(id, noteId, request.IsPinned), cancellationToken));

    [HttpDelete("{id:guid}/notes/{noteId:guid}")]
    [ProducesResponseType<CustomerDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerDto>> RemoveNote(Guid id, Guid noteId, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new RemoveNoteCommand(id, noteId), cancellationToken));
}

public sealed record CreateCustomerRequest(
    string? DisplayNameAr, string? DisplayNameEn, string Type, string? LegalName, string PreferredLanguage,
    IReadOnlyList<CreateCustomerContactRequest>? Contacts = null, bool ConfirmDuplicate = false, Guid? DepartmentId = null);

public sealed record CreateCustomerContactRequest(
    string Name, string? JobTitle = null, string? Email = null, string? Phone = null, bool IsPrimary = false, bool HasPortalAccess = false);

public sealed record UpdateCustomerRequest(
    string? DisplayNameAr, string? DisplayNameEn, string Type, string? LegalName, string PreferredLanguage, byte[] RowVersion);

public sealed record CustomerRowVersionRequest(byte[] RowVersion);

public sealed record AddContactRequest(
    string Name, string? JobTitle, string? Email, string? Phone, bool IsPrimary, bool HasPortalAccess, byte[] RowVersion);

public sealed record UpdateContactRequest(
    string Name, string? JobTitle, string? Email, string? Phone, bool HasPortalAccess, byte[] RowVersion);

public sealed record AddNoteRequest(string Body);

public sealed record PinNoteRequest(bool IsPinned);
