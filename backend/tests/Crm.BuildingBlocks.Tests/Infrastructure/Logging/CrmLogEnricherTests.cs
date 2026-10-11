using System.Security.Claims;
using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Infrastructure.Logging;
using Crm.BuildingBlocks.Tests.Support;
using Crm.BuildingBlocks.Tests.Support.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Core;
using Serilog.Events;

namespace Crm.BuildingBlocks.Tests.Infrastructure.Logging;

public sealed class CrmLogEnricherTests
{
    private static ICorrelationIdAccessor Correlation(string? id)
    {
        var accessor = Substitute.For<ICorrelationIdAccessor>();
        accessor.CorrelationId.Returns(id);
        return accessor;
    }

    private static IHttpContextAccessor ContextAccessor(HttpContext? context)
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(context);
        return accessor;
    }

    private static DefaultHttpContext SignedIn(ICurrentUserService? currentUser, string? userId = "user-9")
    {
        List<Claim> claims = [];
        if (userId is not null)
        {
            claims.Add(new Claim(CrmClaimTypes.UserId, userId));
        }

        var context = HttpContexts.Authenticated(claims, CrmAuthenticationSchemes.Staff);
        var services = new ServiceCollection();
        if (currentUser is not null)
        {
            services.AddSingleton(currentUser);
        }

        context.RequestServices = services.BuildServiceProvider();
        return context;
    }

    private static CollectingSink Run(CrmLogEnricher enricher, Action<Serilog.Core.Logger>? log = null)
    {
        var sink = new CollectingSink();
        using var logger = sink.Logger(c => c.Enrich.With(enricher));
        if (log is null)
        {
            logger.Information("event");
        }
        else
        {
            log(logger);
        }

        return sink;
    }

    [Fact]
    public void TheCorrelationIdIsAddedWhenPresent()
    {
        var sink = Run(new CrmLogEnricher(Correlation("corr-1"), ContextAccessor(null)));

        Assert.Equal("corr-1", CollectingSink.Scalar(sink.Only, "CorrelationId"));
    }

    [Fact]
    public void NoCorrelationIdAddsNoProperty()
    {
        var sink = Run(new CrmLogEnricher(Correlation(null), ContextAccessor(null)));

        Assert.False(sink.Only.Properties.ContainsKey("CorrelationId"));
    }

    [Fact]
    public void AnUnauthenticatedRequestGetsOnlyTheCorrelationId()
    {
        var sink = Run(new CrmLogEnricher(Correlation("corr-1"), ContextAccessor(new DefaultHttpContext())));

        Assert.Equal(["CorrelationId"], sink.Only.Properties.Keys);
    }

    [Fact]
    public void AnAuthenticatedRequestGetsTheUserFromTheSubClaimAndTheScopeFromTheCurrentUser()
    {
        var user = CurrentUsers.Agent(Scopes.AgentIn(ScopeFixtures.DeptA1, ScopeFixtures.BranchA1));
        var sink = Run(new CrmLogEnricher(Correlation("corr-1"), ContextAccessor(SignedIn(user))));

        Assert.Equal("user-9", CollectingSink.Scalar(sink.Only, "UserId"));
        Assert.Equal(ScopeFixtures.TenantAGuid.ToString(), CollectingSink.Scalar(sink.Only, "TenantId"));
        Assert.Equal(ScopeFixtures.DeptA1Guid.ToString(), CollectingSink.Scalar(sink.Only, "DepartmentId"));
        Assert.Equal(ScopeFixtures.BranchA1Guid.ToString(), CollectingSink.Scalar(sink.Only, "BranchId"));
    }

    [Fact]
    public void WithNoCurrentUserServiceRegisteredOnlyTheUserIdIsAdded()
    {
        var sink = Run(new CrmLogEnricher(Correlation(null), ContextAccessor(SignedIn(null))));

        Assert.Equal(["UserId"], sink.Only.Properties.Keys);
    }

    [Fact]
    public void NullScopeValuesAddNothing()
    {
        var user = CurrentUsers.Agent(Scopes.Agent());
        var sink = Run(new CrmLogEnricher(Correlation(null), ContextAccessor(SignedIn(user))));

        Assert.False(sink.Only.Properties.ContainsKey("DepartmentId"));
        Assert.False(sink.Only.Properties.ContainsKey("BranchId"));
        Assert.True(sink.Only.Properties.ContainsKey("TenantId"));
    }

    [Fact]
    public void AnAuthenticatedUserWithoutASubClaimGetsNoUserId()
    {
        var user = CurrentUsers.Agent(Scopes.AgentIn(ScopeFixtures.DeptA1, ScopeFixtures.BranchA1));
        var sink = Run(new CrmLogEnricher(Correlation(null), ContextAccessor(SignedIn(user, userId: null))));

        Assert.False(sink.Only.Properties.ContainsKey("UserId"));
        Assert.True(sink.Only.Properties.ContainsKey("TenantId"));
    }

    [Fact]
    public void AnExistingCorrelationIdPropertyIsNotOverwritten()
    {
        var enricher = new CrmLogEnricher(Correlation("from-accessor"), ContextAccessor(null));
        var sink = new CollectingSink();
        using var logger = sink.Logger(c => c.Enrich.With(enricher));

        logger.ForContext("CorrelationId", "already-there").Information("event");

        Assert.Equal("already-there", CollectingSink.Scalar(sink.Only, "CorrelationId"));
    }

    [Fact]
    public void NullArgumentsAreRefused()
    {
        var enricher = new CrmLogEnricher(Correlation(null), ContextAccessor(null));
        var factory = Substitute.For<ILogEventPropertyFactory>();
        var logEvent = new LogEvent(Clock.Now, LogEventLevel.Information, null, MessageTemplate.Empty, []);

        NullArguments.AssertThrowsArgumentNull(enricher.Enrich, null, factory);
        NullArguments.AssertThrowsArgumentNull(enricher.Enrich, logEvent, null);
    }
}
