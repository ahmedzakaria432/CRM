using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Localization;
using Crm.BuildingBlocks.Presentation.ErrorHandling;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Crm.BuildingBlocks.Tests.Presentation.ErrorHandling;

public sealed class ErrorHandlingServiceCollectionExtensionsTests
{
    private static (ProblemDetailsOptions Options, DefaultHttpContext Context) Build(string? correlationId = null, string path = "/api/agent/things")
    {
        var localization = Substitute.For<ILocalizationService>();
        localization.Get(Arg.Any<string>()).Returns(call => call.Arg<string>());
        var correlation = Substitute.For<ICorrelationIdAccessor>();
        correlation.CorrelationId.Returns(correlationId);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(localization);
        services.AddSingleton(correlation);
        services.AddCrmErrorHandling();
        var provider = services.BuildServiceProvider();

        var context = new DefaultHttpContext { RequestServices = provider };
        context.Request.Path = path;
        return (provider.GetRequiredService<IOptions<ProblemDetailsOptions>>().Value, context);
    }

    private static void Customize(ProblemDetailsOptions options, DefaultHttpContext context, ProblemDetails problem)
    {
        var customize = Assert.IsAssignableFrom<Action<ProblemDetailsContext>>(options.CustomizeProblemDetails);
        customize(new ProblemDetailsContext { HttpContext = context, ProblemDetails = problem });
    }

    [Fact]
    public void AProblemWithNoTypeGetsTheStatusTypeAndALocalisedTitle()
    {
        var (options, context) = Build();
        var problem = new ProblemDetails { Status = 404 };

        Customize(options, context, problem);

        Assert.Equal(ProblemTypes.NotFound, problem.Type);
        Assert.Equal(CrmMessageKeys.ProblemTitle.NotFound, problem.Title);
    }

    [Fact]
    public void WithNoStatusOnTheProblemTheResponseStatusIsUsed()
    {
        var (options, context) = Build();
        context.Response.StatusCode = 405;
        var problem = new ProblemDetails();

        Customize(options, context, problem);

        Assert.Equal(ProblemTypes.MethodNotAllowed, problem.Type);
    }

    [Fact]
    public void TheFrameworkDefaultTypeIsReplaced()
    {
        var (options, context) = Build();
        var problem = new ProblemDetails { Status = 415, Type = "https://tools.ietf.org/html/rfc9110#section-15.5.16" };

        Customize(options, context, problem);

        Assert.Equal(ProblemTypes.UnsupportedMediaType, problem.Type);
        Assert.Equal(CrmMessageKeys.ProblemTitle.UnsupportedMediaType, problem.Title);
    }

    [Fact]
    public void AValidationProblemWithNoTypeBecomesTheValidationType()
    {
        var (options, context) = Build();
        var problem = new ValidationProblemDetails { Status = 400 };

        Customize(options, context, problem);

        Assert.Equal(ProblemTypes.Validation, problem.Type);
        Assert.Equal(CrmMessageKeys.ProblemTitle.Validation, problem.Title);
    }

    [Fact]
    public void ATypeOurOwnCodeAlreadyChoseIsLeftAlone()
    {
        var (options, context) = Build();
        var problem = new ProblemDetails
        {
            Status = 422,
            Type = ProblemTypes.DomainRule,
            Title = "kept title",
        };

        Customize(options, context, problem);

        Assert.Equal(ProblemTypes.DomainRule, problem.Type);
        Assert.Equal("kept title", problem.Title);
    }

    [Fact]
    public void TheInstanceDefaultsToTheRequestPathAndIsNotOverwritten()
    {
        var (options, context) = Build(path: "/api/portal/tickets/7");
        var withNone = new ProblemDetails { Status = 404 };
        var withOwn = new ProblemDetails { Status = 404, Instance = "urn:own" };

        Customize(options, context, withNone);
        Customize(options, context, withOwn);

        Assert.Equal("/api/portal/tickets/7", withNone.Instance);
        Assert.Equal("urn:own", withOwn.Instance);
    }

    [Fact]
    public void TheCorrelationIdExtensionIsAddedOnlyWhenTheAccessorHasOne()
    {
        var (withId, withIdContext) = Build("corr-5");
        var (withoutId, withoutIdContext) = Build();
        var first = new ProblemDetails { Status = 500 };
        var second = new ProblemDetails { Status = 500 };

        Customize(withId, withIdContext, first);
        Customize(withoutId, withoutIdContext, second);

        Assert.Equal("corr-5", first.Extensions["correlationId"]);
        Assert.False(second.Extensions.ContainsKey("correlationId"));
    }

    [Fact]
    public void TheExceptionHandlerIsRegisteredAsTheGlobalExceptionHandler()
    {
        var services = new ServiceCollection();

        services.AddCrmErrorHandling();

        Assert.Contains(
            services,
            d => d.ServiceType == typeof(IExceptionHandler) && d.ImplementationType == typeof(GlobalExceptionHandler));
    }

    [Fact]
    public void ItReturnsTheCollectionForChaining()
    {
        var services = new ServiceCollection();

        Assert.Same(services, services.AddCrmErrorHandling());
    }
}
