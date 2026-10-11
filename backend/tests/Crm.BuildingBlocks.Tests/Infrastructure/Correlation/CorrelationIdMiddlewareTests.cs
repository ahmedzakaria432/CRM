using System.Diagnostics;
using System.Text.RegularExpressions;
using Crm.BuildingBlocks.Infrastructure.Correlation;
using Crm.BuildingBlocks.Tests.Support.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace Crm.BuildingBlocks.Tests.Infrastructure.Correlation;

public sealed partial class CorrelationIdMiddlewareTests
{
    [GeneratedRegex("^[0-9a-f]{32}$")]
    private static partial Regex ThirtyTwoHex();

    private static (DefaultHttpContext Context, RecordingResponseFeature Feature, Func<bool> NextCalled, CorrelationIdMiddleware Middleware) Build(
        string? headerValue = null)
    {
        var feature = new RecordingResponseFeature();
        var context = new DefaultHttpContext();
        context.Features.Set<IHttpResponseFeature>(feature);
        context.Features.Set<IHttpResponseBodyFeature>(feature);

        if (headerValue is not null)
        {
            context.Request.Headers[CorrelationIdMiddleware.HeaderName] = headerValue;
        }

        var called = false;
        var middleware = new CorrelationIdMiddleware(_ =>
        {
            called = true;
            return Task.CompletedTask;
        });

        return (context, feature, () => called, middleware);
    }

    [Theory]
    [InlineData("abc123")]
    [InlineData("req-2026.10.11_09:00")]
    [InlineData("A")]
    public async Task AnAcceptableHeaderIsKept(string header)
    {
        var (context, _, _, middleware) = Build(header);

        await middleware.InvokeAsync(context);

        Assert.Equal(header, context.Items[CorrelationIdMiddleware.ItemKey]);
    }

    [Fact]
    public async Task AHeaderOfExactlySixtyFourCharactersIsKept()
    {
        var header = new string('a', 64);
        var (context, _, _, middleware) = Build(header);

        await middleware.InvokeAsync(context);

        Assert.Equal(header, context.Items[CorrelationIdMiddleware.ItemKey]);
    }

    [Fact]
    public async Task ASixtyFiveCharacterHeaderIsReplaced()
    {
        var header = new string('a', 65);
        var (context, _, _, middleware) = Build(header);

        await middleware.InvokeAsync(context);

        var stored = Assert.IsType<string>(context.Items[CorrelationIdMiddleware.ItemKey]);
        Assert.NotEqual(header, stored);
        Assert.Matches(ThirtyTwoHex(), stored);
    }

    [Theory]
    [InlineData("has space")]
    [InlineData("a/b")]
    [InlineData("tab\there")]
    [InlineData("line\nbreak")]
    [InlineData("quote\"d")]
    [InlineData("café")]
    [InlineData("<script>")]
    public async Task AnUnsafeHeaderIsReplacedNotReflected(string header)
    {
        var (context, _, _, middleware) = Build();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = header;

        await middleware.InvokeAsync(context);

        var stored = Assert.IsType<string>(context.Items[CorrelationIdMiddleware.ItemKey]);
        Assert.NotEqual(header, stored);
        Assert.Matches(ThirtyTwoHex(), stored);
    }

    [Fact]
    public async Task AnEmptyHeaderIsReplaced()
    {
        var (context, _, _, middleware) = Build(string.Empty);

        await middleware.InvokeAsync(context);

        Assert.Matches(ThirtyTwoHex(), Assert.IsType<string>(context.Items[CorrelationIdMiddleware.ItemKey]));
    }

    [Fact]
    public async Task AnAbsentHeaderGeneratesAnId()
    {
        var (context, _, _, middleware) = Build();

        await middleware.InvokeAsync(context);

        Assert.Matches(ThirtyTwoHex(), Assert.IsType<string>(context.Items[CorrelationIdMiddleware.ItemKey]));
    }

    [Fact]
    public async Task AGeneratedIdIsTheCurrentTraceIdWhenAnActivityExists()
    {
        using var activity = new Activity("correlation-test");
        activity.Start();
        var (context, _, _, middleware) = Build();

        await middleware.InvokeAsync(context);

        Assert.Equal(activity.TraceId.ToHexString(), context.Items[CorrelationIdMiddleware.ItemKey]);
        activity.Stop();
    }

    [Fact]
    public async Task TheCurrentActivityIsTaggedWithTheCorrelationId()
    {
        using var activity = new Activity("correlation-test-tag");
        activity.Start();
        var (context, _, _, middleware) = Build("tagged-1");

        await middleware.InvokeAsync(context);

        Assert.Equal("tagged-1", activity.GetTagItem("crm.correlation_id"));
        activity.Stop();
    }

    [Fact]
    public async Task TheIdIsWrittenToTheResponseWhenTheResponseStarts()
    {
        var (context, feature, _, middleware) = Build("echo-me");

        await middleware.InvokeAsync(context);
        Assert.False(feature.Headers.ContainsKey(CorrelationIdMiddleware.HeaderName));
        await feature.RunStartingAsync();

        Assert.Equal("echo-me", feature.Headers[CorrelationIdMiddleware.HeaderName].ToString());
    }

    [Fact]
    public async Task TheNextMiddlewareRuns()
    {
        var (context, _, nextCalled, middleware) = Build();

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled());
    }

    [Fact]
    public void TheHeaderAndItemKeyAreStable()
    {
        Assert.Equal("X-Correlation-Id", CorrelationIdMiddleware.HeaderName);
        Assert.Equal("Crm.CorrelationId", CorrelationIdMiddleware.ItemKey);
    }
}
