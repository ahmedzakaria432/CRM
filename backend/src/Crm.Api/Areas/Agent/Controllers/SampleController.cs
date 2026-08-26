// CANONICAL SLICE - copy this shape for every new use case (CLAUDE.md, "Backend", CQRS).
// DELETE this controller in the story that lands the first real use case (Story 11).

using Crm.Api.Common;
using Crm.Application.Sample.Queries.GetSample;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.Areas.Agent.Controllers;

[Area(CrmAreas.Agent)]
public sealed class SampleController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<SampleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SampleDto>> Get([FromQuery] string? name, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new GetSampleQuery(name ?? string.Empty), cancellationToken));
}
