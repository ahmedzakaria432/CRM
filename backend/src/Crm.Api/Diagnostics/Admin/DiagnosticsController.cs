using Crm.BuildingBlocks.Presentation;
using Crm.SharedKernel.Results;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.Diagnostics.Admin;

/// <summary>
/// Development-only endpoints that produce each failure mode on demand, so every error shape, the log
/// redaction, and the outbound resilience pipeline can be re-verified by hand from
/// backend/requests/diagnostics.http.
/// </summary>
[Area(CrmAreas.Admin)]
[DevelopmentOnly]
public sealed partial class DiagnosticsController(
    ILogger<DiagnosticsController> logger,
    IHttpClientFactory httpClients) : ApiControllerBase
{
    /// <summary>500 - the body must contain neither the message nor the fake connection string.</summary>
    [HttpGet("throw")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public IActionResult Throw() =>
        throw new InvalidOperationException("Simulated failure. Server=diagnostics-db;Password=do-not-leak");

    /// <summary>204 - the log must show every secret-named value as "***".</summary>
    [HttpPost("credentials")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult Credentials([FromBody] DiagnosticsCredentials payload)
    {
        LogCredentials(payload);
        LogScalarApiKey(payload.ApiKey);
        return NoContent();
    }

    [HttpGet("forbidden")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public ActionResult Forbidden() =>
        Problem(Error.Forbidden("Diagnostics.Forbidden", "Simulated forbidden result."));

    [HttpGet("unauthorized")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public new IActionResult Unauthorized() => base.Unauthorized();

    /// <summary>The endpoint that fails on demand: 503 when <paramref name="fail"/>, otherwise 200.</summary>
    [HttpGet("unstable")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public IActionResult Unstable([FromQuery] bool fail = true) =>
        fail ? StatusCode(StatusCodes.Status503ServiceUnavailable) : Ok();

    /// <summary>Calls <see cref="Unstable"/> through the resilient client: retries, then an open circuit.</summary>
    [HttpGet("outbound")]
    [ProducesResponseType<OutboundResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<OutboundResult>> Outbound([FromQuery] bool fail = true, CancellationToken cancellationToken = default)
    {
        HttpClient client = httpClients.CreateClient(DiagnosticsHttpClient.Name);
        using HttpResponseMessage response = await client.GetAsync(
            new Uri($"api/Admin/Diagnostics/unstable?fail={(fail ? "true" : "false")}", UriKind.Relative), cancellationToken);

        response.EnsureSuccessStatusCode();
        return Ok(new OutboundResult("downstream ok"));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Received credentials payload {@Payload}")]
    private partial void LogCredentials(DiagnosticsCredentials payload);

    [LoggerMessage(Level = LogLevel.Information, Message = "Scalar api key {ApiKey}")]
    private partial void LogScalarApiKey(string apiKey);
}

public sealed record DiagnosticsCredentials(string Username, string Password, string ApiKey, string Token, DiagnosticsNested Nested);

public sealed record DiagnosticsNested(string ClientSecret, string Note);

public sealed record OutboundResult(string Status);
