// CANONICAL SLICE - copy this shape for every new use case (CLAUDE.md, "Backend", CQRS).
// DELETE this controller in the story that lands the first real use case (Story 11).

using Crm.BuildingBlocks.Presentation;
using Crm.Modules.Sample.Application;
using Crm.Modules.Sample.Application.Commands.CreateSample;
using Crm.Modules.Sample.Application.Commands.DeleteSample;
using Crm.Modules.Sample.Application.Commands.RenameSample;
using Crm.Modules.Sample.Application.Queries.GetSampleById;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Modules.Sample.Presentation.Controllers.Admin;

/// <summary>Demonstrates the persistence baseline end to end: round-trip of a value object and a
/// child entity, auditing, soft delete, and optimistic concurrency (Story 02). Deleted alongside
/// the rest of the Sample surface in Story 11.</summary>
[Area(CrmAreas.Admin)]
public sealed class SamplesController : ApiControllerBase
{
    [HttpPost]
    [ProducesResponseType<SampleAggregateDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SampleAggregateDto>> Create(
        [FromBody] CreateSampleRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new CreateSampleCommand(request.Name, request.Lines), cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<SampleAggregateDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SampleAggregateDto>> GetById(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new GetSampleByIdQuery(id), cancellationToken));

    [HttpPut("{id:guid}")]
    [ProducesResponseType<SampleAggregateDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SampleAggregateDto>> Rename(
        Guid id, [FromBody] RenameSampleRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new RenameSampleCommand(id, request.Name, request.RowVersion), cancellationToken));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new DeleteSampleCommand(id), cancellationToken));
}

public sealed record CreateSampleRequest(string Name, IReadOnlyList<string> Lines);

/// <summary>RowVersion is base64 over the wire (byte[] serialises that way) - send back exactly
/// what the GET returned; re-encoding it produces a spurious 409.</summary>
public sealed record RenameSampleRequest(string Name, byte[] RowVersion);
