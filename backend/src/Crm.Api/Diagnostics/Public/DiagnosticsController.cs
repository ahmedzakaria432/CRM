using Crm.BuildingBlocks.Presentation;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.Diagnostics.Public;

/// <summary>A Public-Area target for the rate limiter before Story 55 adds real Public endpoints.
/// Development only.</summary>
[Area(CrmAreas.Public)]
[DevelopmentOnly]
public sealed class DiagnosticsController : ApiControllerBase
{
    [HttpGet("ping")]
    [ProducesResponseType<string>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public ActionResult<string> Ping() => Ok("pong");
}
