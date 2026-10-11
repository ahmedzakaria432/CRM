using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Localization;
using Crm.BuildingBlocks.Presentation.ErrorHandling;
using Crm.BuildingBlocks.Presentation.Scoping;
using Crm.BuildingBlocks.Tests.Support;
using Crm.BuildingBlocks.Tests.Support.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Crm.BuildingBlocks.Tests.Presentation.Scoping;

public sealed class OrganisationScopeMiddlewareTests
{
    private static readonly ScopeMembership AgentA1 = Scopes.Member(ScopeFixtures.DeptA1, ScopeFixtures.BranchA1);

    private static ILocalizationService Localization()
    {
        var localization = Substitute.For<ILocalizationService>();
        localization.Get(Arg.Any<string>()).Returns(call => call.Arg<string>());
        return localization;
    }

    private static ICurrentUserService Rejected() => CurrentUsers.Agent(Scopes.Rejected([AgentA1]), "user-3");

    private static ICurrentUserService Accepted() => CurrentUsers.Agent(Scopes.Agent(AgentA1), "user-3");

    [Fact]
    public async Task ANormalScopePassesThroughAndWritesNothing()
    {
        var called = false;
        var middleware = new OrganisationScopeMiddleware(_ =>
        {
            called = true;
            return Task.CompletedTask;
        });
        var problems = new RecordingProblemDetailsService();
        var context = new DefaultHttpContext();

        await middleware.InvokeAsync(context, Accepted(), problems, Localization(), new RecordingLogger<OrganisationScopeMiddleware>());

        Assert.True(called);
        Assert.Empty(problems.Written);
        Assert.Equal(200, context.Response.StatusCode);
    }

    [Fact]
    public async Task ARejectedScopeStopsThePipelineWithA403Problem()
    {
        var called = false;
        var middleware = new OrganisationScopeMiddleware(_ =>
        {
            called = true;
            return Task.CompletedTask;
        });
        var problems = new RecordingProblemDetailsService();
        var context = new DefaultHttpContext();

        await middleware.InvokeAsync(context, Rejected(), problems, Localization(), new RecordingLogger<OrganisationScopeMiddleware>());

        Assert.False(called);
        Assert.Equal(403, context.Response.StatusCode);
        var written = Assert.Single(problems.Written);
        Assert.Same(context, written.HttpContext);
        Assert.Equal(403, written.ProblemDetails.Status);
        Assert.Equal(ProblemTypes.Forbidden, written.ProblemDetails.Type);
        Assert.Equal(CrmMessageKeys.ProblemTitle.Forbidden, written.ProblemDetails.Title);
        Assert.Equal(CrmMessageKeys.ProblemDetail.ScopeRejected, written.ProblemDetails.Detail);
        Assert.Equal("Organisation.ActiveScopeRejected", written.ProblemDetails.Extensions["code"]);
    }

    [Fact]
    public async Task TheRejectionIsLoggedAsAWarningNamingTheHeaderAndTheUser()
    {
        var middleware = new OrganisationScopeMiddleware(_ => Task.CompletedTask);
        var logger = new RecordingLogger<OrganisationScopeMiddleware>();
        var context = new DefaultHttpContext();
        context.Request.Headers[OrganisationScope.HeaderName] = "d3d3d3d3-0000-0000-0000-0000000000b1";

        await middleware.InvokeAsync(context, Rejected(), new RecordingProblemDetailsService(), Localization(), logger);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains("d3d3d3d3-0000-0000-0000-0000000000b1", entry.Message, StringComparison.Ordinal);
        Assert.Contains("user-3", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UseCrmOrganisationScopeRegistersTheMiddleware()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Rejected());
        var problems = new RecordingProblemDetailsService();
        services.AddSingleton<IProblemDetailsService>(problems);
        services.AddSingleton(Localization());
        using var provider = services.BuildServiceProvider();
        var app = new ApplicationBuilder(provider);
        var reachedEndpoint = false;
        app.UseCrmOrganisationScope();
        app.Run(_ =>
        {
            reachedEndpoint = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext { RequestServices = provider };
        await app.Build()(context);

        Assert.False(reachedEndpoint);
        Assert.Equal(403, context.Response.StatusCode);
        Assert.Single(problems.Written);
    }
}
