using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Infrastructure.Correlation;
using Crm.BuildingBlocks.Tests.Support.Http;

namespace Crm.BuildingBlocks.Tests.Infrastructure.Correlation;

public sealed class CorrelationIdDelegatingHandlerTests
{
    private static (HttpMessageInvoker Invoker, StubHandler Inner) Build(string? correlationId)
    {
        var accessor = Substitute.For<ICorrelationIdAccessor>();
        accessor.CorrelationId.Returns(correlationId);
        var inner = new StubHandler();
        var handler = new CorrelationIdDelegatingHandler(accessor) { InnerHandler = inner };
        return (new HttpMessageInvoker(handler), inner);
    }

    [Fact]
    public async Task TheCurrentCorrelationIdIsForwarded()
    {
        var (invoker, inner) = Build("corr-1");
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://example.test/");

        await invoker.SendAsync(request, TestContext.Current.CancellationToken);

        var sent = Assert.Single(inner.Requests);
        Assert.Equal("corr-1", Assert.Single(sent.Headers.GetValues(CorrelationIdMiddleware.HeaderName)));
    }

    [Fact]
    public async Task AnExplicitHeaderIsLeftAlone()
    {
        var (invoker, inner) = Build("corr-1");
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://example.test/");
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, "explicit");

        await invoker.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal("explicit", Assert.Single(Assert.Single(inner.Requests).Headers.GetValues(CorrelationIdMiddleware.HeaderName)));
    }

    [Fact]
    public async Task NoCorrelationIdAddsNoHeader()
    {
        var (invoker, inner) = Build(null);
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://example.test/");

        await invoker.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.False(Assert.Single(inner.Requests).Headers.Contains(CorrelationIdMiddleware.HeaderName));
    }

    [Fact]
    public async Task TheInnerResponseIsReturned()
    {
        var (invoker, _) = Build("corr-1");
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://example.test/");

        using var response = await invoker.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }
}
