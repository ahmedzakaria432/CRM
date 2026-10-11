using Crm.BuildingBlocks.Infrastructure.Correlation;
using Microsoft.AspNetCore.Http;

namespace Crm.BuildingBlocks.Tests.Infrastructure.Correlation;

public sealed class CorrelationIdAccessorTests
{
    private static CorrelationIdAccessor Accessor(HttpContext? context = null)
    {
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        httpContextAccessor.HttpContext.Returns(context);
        return new CorrelationIdAccessor(httpContextAccessor);
    }

    [Fact]
    public void WithNothingSetThereIsNoCorrelationId()
    {
        Assert.Null(Accessor().CorrelationId);
    }

    [Fact]
    public void TheIdTheMiddlewareStoredOnTheRequestIsReturned()
    {
        var context = new DefaultHttpContext();
        context.Items[CorrelationIdMiddleware.ItemKey] = "from-request";

        Assert.Equal("from-request", Accessor(context).CorrelationId);
    }

    [Fact]
    public void ANonStringItemIsIgnored()
    {
        var context = new DefaultHttpContext();
        context.Items[CorrelationIdMiddleware.ItemKey] = 42;

        Assert.Null(Accessor(context).CorrelationId);
    }

    [Fact]
    public void AScopeOverridesTheRequestIdAndRestoresItOnDispose()
    {
        var context = new DefaultHttpContext();
        context.Items[CorrelationIdMiddleware.ItemKey] = "from-request";
        var accessor = Accessor(context);

        using (accessor.BeginScope("from-scope"))
        {
            Assert.Equal("from-scope", accessor.CorrelationId);
        }

        Assert.Equal("from-request", accessor.CorrelationId);
    }

    [Fact]
    public void NestedScopesRestoreInOrder()
    {
        var accessor = Accessor();

        using (accessor.BeginScope("outer"))
        {
            using (accessor.BeginScope("inner"))
            {
                Assert.Equal("inner", accessor.CorrelationId);
            }

            Assert.Equal("outer", accessor.CorrelationId);
        }

        Assert.Null(accessor.CorrelationId);
    }

    [Fact]
    public async Task TheOverrideFlowsAcrossAwaits()
    {
        var accessor = Accessor();

        using (accessor.BeginScope("flowing"))
        {
            await Task.Yield();
            Assert.Equal("flowing", accessor.CorrelationId);
            await Task.Yield();
            Assert.Equal("flowing", accessor.CorrelationId);
        }
    }

    [Fact]
    public async Task ASeparateAsyncFlowDoesNotLeakItsScopeBack()
    {
        var accessor = Accessor();

        await Task.Run(
            () =>
            {
                using (accessor.BeginScope("inside-task"))
                {
                    Assert.Equal("inside-task", accessor.CorrelationId);
                }
            },
            TestContext.Current.CancellationToken);

        Assert.Null(accessor.CorrelationId);
    }

    [Fact]
    public async Task ConcurrentFlowsEachSeeTheirOwnId()
    {
        var accessor = Accessor();

        async Task<string?> Run(string id)
        {
            using (accessor.BeginScope(id))
            {
                await Task.Yield();
                return accessor.CorrelationId;
            }
        }

        var results = await Task.WhenAll(Task.Run(() => Run("one"), TestContext.Current.CancellationToken), Task.Run(() => Run("two"), TestContext.Current.CancellationToken));

        Assert.Equal(["one", "two"], results);
    }
}
