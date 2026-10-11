using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Infrastructure.Correlation;
using Crm.BuildingBlocks.Infrastructure.Http;
using Crm.BuildingBlocks.Tests.Support;
using Crm.BuildingBlocks.Tests.Support.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

namespace Crm.BuildingBlocks.Tests.Infrastructure.Http;

public sealed class HttpClientBuilderExtensionsTests
{
    private static IConfiguration Configuration(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();

    private static IConfiguration Minimal() => Configuration(("Resilience:Outbound:Retry:MaxRetryAttempts", "3"));

    private static ServiceProvider Build(StubHandler stub, string? correlationId, IConfiguration configuration)
    {
        var accessor = Substitute.For<ICorrelationIdAccessor>();
        accessor.CorrelationId.Returns(correlationId);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(accessor);
        services.AddTransient<CorrelationIdDelegatingHandler>();
        services.AddHttpClient("x")
            .AddCrmResilience(configuration)
            .ConfigurePrimaryHttpMessageHandler(() => stub);
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task TheClientPipelineStampsTheCorrelationIdAndReachesTheInnerHandler()
    {
        var stub = new StubHandler();
        using var provider = Build(stub, "corr-77", Minimal());
        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("x");

        using var response = await client.GetAsync("http://example.test/ping", TestContext.Current.CancellationToken);

        Assert.True(response.IsSuccessStatusCode);
        var sent = Assert.Single(stub.Requests);
        Assert.Equal("corr-77", Assert.Single(sent.Headers.GetValues(CorrelationIdMiddleware.HeaderName)));
    }

    [Fact]
    public async Task WithoutACorrelationIdNoHeaderIsStamped()
    {
        var stub = new StubHandler();
        using var provider = Build(stub, null, Minimal());
        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("x");

        using var response = await client.GetAsync("http://example.test/ping", TestContext.Current.CancellationToken);

        Assert.False(Assert.Single(stub.Requests).Headers.Contains(CorrelationIdMiddleware.HeaderName));
    }

    [Fact]
    public void TheOutboundResilienceSectionIsBoundToTheStandardPipeline()
    {
        using var provider = Build(
            new StubHandler(),
            null,
            Configuration(("Resilience:Outbound:Retry:MaxRetryAttempts", "7")));

        var options = provider.GetRequiredService<IOptionsMonitor<HttpStandardResilienceOptions>>().Get("x-standard");

        Assert.Equal(7, options.Retry.MaxRetryAttempts);
    }

    [Fact]
    public void AMissingResilienceSectionFailsFastAtRegistration()
    {
        // Pins the observed behaviour: Configure(section) refuses an absent "Resilience:Outbound" section, so every
        // environment must carry it (appsettings.json does). Recorded as a Follow-up of Story 60.
        var exception = Assert.Throws<ArgumentException>(() => Build(new StubHandler(), null, Configuration()));

        Assert.Contains("section", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NullArgumentsAreRefused()
    {
        var services = new ServiceCollection();
        var builder = services.AddHttpClient("y");
        Func<IHttpClientBuilder, IConfiguration, IHttpClientBuilder> method = Crm.BuildingBlocks.Infrastructure.Http.HttpClientBuilderExtensions.AddCrmResilience;

        NullArguments.AssertThrowsArgumentNull(method, null, Configuration());
        NullArguments.AssertThrowsArgumentNull(method, builder, null);
    }
}
