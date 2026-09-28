using Crm.SharedKernel.Results;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.BuildingBlocks.Presentation;

/// <summary>
/// Every controller derives from this and carries its own [Area(CrmAreas.X)] attribute - the
/// [area] token in the route template expands from that attribute, so a controller that forgets it
/// produces a literal "/api/[area]/..." route that 404s and appears in no Swagger document
/// (verified by probe during planning).
/// </summary>
[ApiController]
[Route("api/[area]/[controller]")]
public abstract class ApiControllerBase : ControllerBase
{
    private ISender? _sender;

    protected ISender Sender => _sender ??= HttpContext.RequestServices.GetRequiredService<ISender>();

    protected ActionResult<T> ToActionResult<T>(Result<T> result) =>
        result.IsSuccess ? Ok(result.Value) : result.Error.ToActionResult();

    protected ActionResult ToActionResult(Result result) =>
        result.IsSuccess ? NoContent() : result.Error.ToActionResult();
}
