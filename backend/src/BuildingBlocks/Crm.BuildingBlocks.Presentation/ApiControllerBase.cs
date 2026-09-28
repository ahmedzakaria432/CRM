using Crm.BuildingBlocks.Presentation.ErrorHandling;
using Crm.SharedKernel.Results;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
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
        result.IsSuccess ? Ok(result.Value) : Problem(result.Error);

    protected ActionResult ToActionResult(Result result) =>
        result.IsSuccess ? NoContent() : Problem(result.Error);

    /// <summary>
    /// Built through ProblemDetailsFactory, not <c>new ProblemDetails</c>, so the one customisation in
    /// AddCrmErrorHandling (correlationId, instance) applies to Result failures exactly as it does to exceptions.
    /// </summary>
    protected ActionResult Problem(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        ProblemDetailsFactory factory = HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>();
        int status = ErrorTypeMapping.ToStatusCode(error.Type);
        string type = ErrorTypeMapping.ToProblemType(error.Type);

        if (error.Type == ErrorType.Validation)
        {
            var modelState = new ModelStateDictionary();
            foreach ((string field, string[] messages) in ValidationErrorParser.Parse(error.Description))
            {
                foreach (string message in messages)
                {
                    modelState.AddModelError(field, message);
                }
            }

            ValidationProblemDetails validation = factory.CreateValidationProblemDetails(
                HttpContext, modelState, status, ProblemTypes.TitleFor(type), type, error.Description);
            validation.Extensions["code"] = error.Code;

            return new BadRequestObjectResult(validation);
        }

        ProblemDetails problem = factory.CreateProblemDetails(
            HttpContext, status, ProblemTypes.TitleFor(type), type, error.Description);
        problem.Extensions["code"] = error.Code;

        return new ObjectResult(problem) { StatusCode = status };
    }
}
