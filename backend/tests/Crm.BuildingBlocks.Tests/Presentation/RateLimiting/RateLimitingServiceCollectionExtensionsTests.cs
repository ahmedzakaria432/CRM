using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Localization;
using Crm.BuildingBlocks.Presentation.ErrorHandling;
using Crm.BuildingBlocks.Presentation.RateLimiting;
using Crm.BuildingBlocks.Tests.Support;
using Crm.BuildingBlocks.Tests.Support.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Crm.BuildingBlocks.Tests.Presentation.RateLimiting;

public sealed class RateLimitingServiceCollectionExtensionsTests
{
    private static readonly string[] Sections = ["Public", "SignIn", "Portal", "PortalSignIn"];

    private static IConfiguration Configuration(IEnumerable<string>? sections = null, int permitLimit = 2) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection((sections ?? Sections).SelectMany(section => new[]
            {
                new KeyValuePair<string, string?>($"RateLimiting:{section}:PermitLimit", permitLimit.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new KeyValuePair<string, string?>($"RateLimiting:{section}:Window", "00:01:00"),
            }))
            .Build();

    private static (RateLimiterOptions Options, ServiceProvider Provider) Build(
        RecordingProblemDetailsService? problems = null, int permitLimit = 2)
    {
        var localization = Substitute.For<ILocalizationService>();
        localization.Get(Arg.Any<string>()).Returns(call => call.Arg<string>());

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(localization);
        services.AddSingleton<IProblemDetailsService>(problems ?? new RecordingProblemDetailsService());
        services.AddCrmRateLimiting(Configuration(permitLimit: permitLimit));
        var provider = services.BuildServiceProvider();
        return (provider.GetRequiredService<IOptions<RateLimiterOptions>>().Value, provider);
    }

    private static DefaultHttpContext ContextFor(string? area, string ip, string? user = null)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(ip);
        if (area is not null)
        {
            context.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(new AreaAttribute(area)), "test"));
        }

        if (user is not null)
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(CrmClaimTypes.UserId, user)], "Portal"));
        }

        return context;
    }

    private static PartitionedRateLimiter<HttpContext> Global(RateLimiterOptions options) =>
        Assert.IsAssignableFrom<PartitionedRateLimiter<HttpContext>>(options.GlobalLimiter);

    private static bool Acquire(PartitionedRateLimiter<HttpContext> limiter, HttpContext context)
    {
        using var lease = limiter.AttemptAcquire(context);
        return lease.IsAcquired;
    }

    // --- configuration ------------------------------------------------------------------------------------

    [Theory]
    [InlineData("Public")]
    [InlineData("SignIn")]
    [InlineData("Portal")]
    [InlineData("PortalSignIn")]
    public void AMissingSectionFailsNamingIt(string missing)
    {
        var configuration = Configuration(Sections.Where(s => s != missing));
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() => services.AddCrmRateLimiting(configuration));

        Assert.Contains($"RateLimiting:{missing} is not configured", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectedRequestsGetA429()
    {
        var (options, provider) = Build();
        using var _ = provider;

        Assert.Equal(StatusCodes.Status429TooManyRequests, options.RejectionStatusCode);
    }

    [Fact]
    public void ThePolicyNamesAreStable()
    {
        Assert.Equal("sign-in", CrmRateLimitPolicies.SignIn);
        Assert.Equal("portal-sign-in", CrmRateLimitPolicies.PortalSignIn);
    }

    [Fact]
    public void SettingsRecordsHaveValueEquality()
    {
        Assert.Equal(new PublicRateLimitSettings(5, TimeSpan.FromMinutes(1)), new PublicRateLimitSettings(5, TimeSpan.FromMinutes(1)));
        Assert.NotEqual(new PublicRateLimitSettings(5, TimeSpan.FromMinutes(1)), new PublicRateLimitSettings(6, TimeSpan.FromMinutes(1)));
    }

    // --- the global limiter -------------------------------------------------------------------------------

    [Fact]
    public void APublicEndpointAllowsThePermitLimitPerIpThenRefuses()
    {
        var (options, provider) = Build(permitLimit: 2);
        using var _ = provider;
        var limiter = Global(options);

        Assert.True(Acquire(limiter, ContextFor("Public", "10.0.0.1")));
        Assert.True(Acquire(limiter, ContextFor("Public", "10.0.0.1")));
        Assert.False(Acquire(limiter, ContextFor("Public", "10.0.0.1")));
    }

    [Fact]
    public void AnotherIpHasItsOwnPublicBucket()
    {
        var (options, provider) = Build(permitLimit: 1);
        using var _ = provider;
        var limiter = Global(options);

        Assert.True(Acquire(limiter, ContextFor("Public", "10.0.0.1")));
        Assert.False(Acquire(limiter, ContextFor("Public", "10.0.0.1")));
        Assert.True(Acquire(limiter, ContextFor("Public", "10.0.0.2")));
    }

    [Fact]
    public void ThePortalIsLimitedPerSignedInUserNotPerIp()
    {
        var (options, provider) = Build(permitLimit: 1);
        using var _ = provider;
        var limiter = Global(options);

        Assert.True(Acquire(limiter, ContextFor("Portal", "10.0.0.1", "alice")));
        Assert.False(Acquire(limiter, ContextFor("Portal", "10.0.0.1", "alice")));
        Assert.True(Acquire(limiter, ContextFor("Portal", "10.0.0.1", "bob")));
    }

    [Fact]
    public void AnAnonymousPortalCallerIsLimitedPerIp()
    {
        var (options, provider) = Build(permitLimit: 1);
        using var _ = provider;
        var limiter = Global(options);

        Assert.True(Acquire(limiter, ContextFor("Portal", "10.0.0.1")));
        Assert.False(Acquire(limiter, ContextFor("Portal", "10.0.0.1")));
        Assert.True(Acquire(limiter, ContextFor("Portal", "10.0.0.2")));
    }

    [Fact]
    public void ThePublicAndPortalBucketsDoNotShareACounter()
    {
        var (options, provider) = Build(permitLimit: 1);
        using var _ = provider;
        var limiter = Global(options);

        Assert.True(Acquire(limiter, ContextFor("Public", "10.0.0.1")));
        Assert.True(Acquire(limiter, ContextFor("Portal", "10.0.0.1")));
    }

    [Theory]
    [InlineData("Agent")]
    [InlineData("Admin")]
    [InlineData(null)]
    public void OtherAreasAndEndpointsWithoutAnAreaAreNeverLimited(string? area)
    {
        var (options, provider) = Build(permitLimit: 1);
        using var _ = provider;
        var limiter = Global(options);

        for (var i = 0; i < 50; i++)
        {
            Assert.True(Acquire(limiter, ContextFor(area, "10.0.0.1")));
        }
    }

    [Fact]
    public void TheAreaComparisonIsOrdinal()
    {
        var (options, provider) = Build(permitLimit: 1);
        using var _ = provider;
        var limiter = Global(options);

        for (var i = 0; i < 5; i++)
        {
            Assert.True(Acquire(limiter, ContextFor("public", "10.0.0.1")));
        }
    }

    // --- named policies -----------------------------------------------------------------------------------

    [Fact]
    public void TheSignInPoliciesAreRegistered()
    {
        var (options, provider) = Build();
        using var _ = provider;
        var policyMap = typeof(RateLimiterOptions)
            .GetProperty("PolicyMap", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            ?.GetValue(options);

        var keys = Assert.IsAssignableFrom<System.Collections.IDictionary>(policyMap).Keys.Cast<string>().ToList();

        Assert.Contains(CrmRateLimitPolicies.SignIn, keys);
        Assert.Contains(CrmRateLimitPolicies.PortalSignIn, keys);
    }

    // --- OnRejected ---------------------------------------------------------------------------------------

    private static async Task<(DefaultHttpContext Context, RecordingProblemDetailsService Problems)> RejectAsync(TimeSpan? retryAfter)
    {
        var problems = new RecordingProblemDetailsService();
        var (options, provider) = Build(problems);
        using var _ = provider;
        var context = new DefaultHttpContext { RequestServices = provider };
        var onRejected = Assert.IsAssignableFrom<Func<OnRejectedContext, CancellationToken, ValueTask>>(options.OnRejected);

        await onRejected(
            new OnRejectedContext { HttpContext = context, Lease = new FakeLease(retryAfter) },
            TestContext.Current.CancellationToken);

        return (context, problems);
    }

    [Fact]
    public async Task ARejectionWritesA429RateLimitedProblem()
    {
        var (_, problems) = await RejectAsync(TimeSpan.FromSeconds(10));

        var problem = Assert.Single(problems.Written).ProblemDetails;
        Assert.Equal(429, problem.Status);
        Assert.Equal(ProblemTypes.RateLimited, problem.Type);
        Assert.Equal(CrmMessageKeys.ProblemTitle.RateLimited, problem.Title);
        Assert.Equal(CrmMessageKeys.ProblemDetail.RateLimited, problem.Detail);
    }

    [Theory]
    [InlineData(1.0, "1")]
    [InlineData(1.2, "2")]
    [InlineData(59.01, "60")]
    [InlineData(0.1, "1")]
    public async Task RetryAfterIsTheCeilingOfTheLeasesRetryAfterSeconds(double seconds, string expected)
    {
        var (context, _) = await RejectAsync(TimeSpan.FromSeconds(seconds));

        Assert.Equal(expected, context.Response.Headers.RetryAfter.ToString());
    }

    [Fact]
    public async Task RetryAfterIsFormattedInTheInvariantCultureEvenUnderArabic()
    {
        using var culture = UiCulture.Use("ar");

        var (context, _) = await RejectAsync(TimeSpan.FromSeconds(12.5));

        Assert.Equal("13", context.Response.Headers.RetryAfter.ToString());
    }

    [Fact]
    public async Task WithNoRetryAfterMetadataNoHeaderIsWrittenButTheProblemStillIs()
    {
        var (context, problems) = await RejectAsync(null);

        Assert.False(context.Response.Headers.ContainsKey("Retry-After"));
        Assert.Single(problems.Written);
    }

    [Fact]
    public void ItReturnsTheCollectionForChaining()
    {
        var services = new ServiceCollection();

        Assert.Same(services, services.AddCrmRateLimiting(Configuration()));
    }
}
