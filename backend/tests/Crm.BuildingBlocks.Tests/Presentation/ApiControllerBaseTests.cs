using System.Reflection;
using Crm.BuildingBlocks.Application.Localization;
using Crm.BuildingBlocks.Presentation;
using Crm.BuildingBlocks.Presentation.ErrorHandling;
using Crm.BuildingBlocks.Tests.Support;
using Crm.SharedKernel.Results;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.BuildingBlocks.Tests.Presentation;

public sealed class TestController : ApiControllerBase
{
    public ActionResult<T> Wrap<T>(Result<T> result) => ToActionResult(result);

    public ActionResult WrapPlain(Result result) => ToActionResult(result);

    public ActionResult Fail(Error error) => Problem(error);

    public ISender ExposedSender => Sender;
}

public sealed class ApiControllerBaseTests
{
    private static (TestController Controller, ISender Sender, Func<int> SenderResolutions) Build()
    {
        var localization = Substitute.For<ILocalizationService>();
        localization.Get(Arg.Any<string>()).Returns(call => call.Arg<string>());
        var sender = Substitute.For<ISender>();
        var resolutions = 0;

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddControllers();
        services.AddSingleton(localization);
        services.AddTransient(_ =>
        {
            resolutions++;
            return sender;
        });
        var provider = services.BuildServiceProvider();

        var controller = new TestController
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { RequestServices = provider } },
        };
        return (controller, sender, () => resolutions);
    }

    private static ILocalizationService EchoLocalization()
    {
        var localization = Substitute.For<ILocalizationService>();
        localization.Get(Arg.Any<string>()).Returns(call => call.Arg<string>());
        return localization;
    }

    private static ProblemDetails ProblemOf(ActionResult result) => result switch
    {
        BadRequestObjectResult { Value: ProblemDetails validation } => validation,
        ObjectResult { Value: ProblemDetails problem } => problem,
        _ => throw new InvalidOperationException("not a problem result"),
    };

    [Fact]
    public void ASuccessfulTypedResultIsOkWithTheValue()
    {
        var (controller, _, _) = Build();

        var result = controller.Wrap(Result.Success(42));

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(42, ok.Value);
    }

    [Fact]
    public void AFailedTypedResultIsAProblemResult()
    {
        var (controller, _, _) = Build();

        var result = controller.Wrap(Result.Failure<int>(Error.NotFound("Thing.NotFound", "no such thing")));

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(404, objectResult.StatusCode);
        Assert.IsAssignableFrom<ProblemDetails>(objectResult.Value);
    }

    [Fact]
    public void ASuccessfulPlainResultIsNoContent()
    {
        var (controller, _, _) = Build();

        Assert.IsType<NoContentResult>(controller.WrapPlain(Result.Success()));
    }

    [Fact]
    public void AFailedPlainResultIsAProblemResult()
    {
        var (controller, _, _) = Build();

        var result = controller.WrapPlain(Result.Failure(Error.Conflict("Thing.Conflict", "clash")));

        Assert.Equal(409, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    [Theory]
    [InlineData(ErrorType.NotFound, 404, "urn:crm:problem:not-found")]
    [InlineData(ErrorType.Conflict, 409, "urn:crm:problem:conflict")]
    [InlineData(ErrorType.Forbidden, 403, "urn:crm:problem:forbidden")]
    [InlineData(ErrorType.Domain, 422, "urn:crm:problem:domain-rule")]
    [InlineData(ErrorType.Unauthorized, 401, "urn:crm:problem:unauthorized")]
    [InlineData(ErrorType.Failure, 500, "urn:crm:problem:internal")]
    public void EveryErrorTypeBecomesAProblemWithItsStatusTypeCodeAndDetail(ErrorType type, int status, string problemType)
    {
        var (controller, _, _) = Build();
        var error = new Error("Some.Code", "what happened", type);

        var result = controller.Fail(error);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(status, objectResult.StatusCode);
        var problem = ProblemOf(result);
        Assert.Equal(status, problem.Status);
        Assert.Equal(problemType, problem.Type);
        Assert.Equal("what happened", problem.Detail);
        Assert.Equal("Some.Code", problem.Extensions["code"]);
        Assert.Equal(ProblemTypes.TitleFor(problemType, EchoLocalization()), problem.Title);
    }

    [Fact]
    public void AValidationErrorIsABadRequestWithTheFieldErrorsParsedFromTheDescription()
    {
        var (controller, _, _) = Build();
        var error = Error.Validation("Validation.Failed", "Name: Name is required. | Email: bad | Name: too short");

        var result = controller.Fail(error);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var validation = Assert.IsType<ValidationProblemDetails>(badRequest.Value);
        Assert.Equal(400, validation.Status);
        Assert.Equal(ProblemTypes.Validation, validation.Type);
        Assert.Equal(["Name is required.", "too short"], validation.Errors["Name"]);
        Assert.Equal(["bad"], validation.Errors["Email"]);
        Assert.Equal("Validation.Failed", validation.Extensions["code"]);
        Assert.Equal(error.Description, validation.Detail);
    }

    [Fact]
    public void TheTitleIsLocalised()
    {
        var (controller, _, _) = Build();

        var problem = ProblemOf(controller.Fail(Error.NotFound("X", "y")));

        Assert.Equal(CrmMessageKeys.ProblemTitle.NotFound, problem.Title);
    }

    [Fact]
    public void ProblemRejectsANullError()
    {
        var (controller, _, _) = Build();
        Func<Error, ActionResult> method = controller.Fail;

        NullArguments.AssertThrowsArgumentNull(method, new object?[] { null });
    }

    [Fact]
    public void TheSenderIsResolvedOnceFromTheRequestServices()
    {
        var (controller, sender, resolutions) = Build();

        var first = controller.ExposedSender;
        var second = controller.ExposedSender;

        Assert.Same(sender, first);
        Assert.Same(first, second);
        Assert.Equal(1, resolutions());
    }

    [Fact]
    public void TheBaseClassCarriesTheApiControllerAndTheFrontendFacingRouteTemplate()
    {
        var type = typeof(ApiControllerBase);

        Assert.NotNull(type.GetCustomAttribute<ApiControllerAttribute>());
        Assert.Equal("api/[area]/[controller]", type.GetCustomAttribute<RouteAttribute>()?.Template);
        Assert.True(type.IsAbstract);
    }
}
