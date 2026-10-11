using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Infrastructure.Correlation;
using Crm.BuildingBlocks.Infrastructure.Observability;
using Crm.BuildingBlocks.Tests.Support;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog.Core;

namespace Crm.BuildingBlocks.Tests.Infrastructure.Observability;

/// <summary>Registration only. Resolving the Serilog logger would write to the console and the OpenTelemetry
/// providers would start exporters, so nothing here builds them.</summary>
public sealed class ObservabilityServiceCollectionExtensionsTests
{
    private static IConfiguration Configuration(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();

    private static IHostEnvironment Environment()
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.ApplicationName.Returns("Crm.Tests");
        environment.EnvironmentName.Returns("Production");
        return environment;
    }

    [Fact]
    public void ItRegistersTheCorrelationAccessorAsASingleton()
    {
        var services = new ServiceCollection();

        services.AddCrmObservability(Configuration(), Environment());

        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(ICorrelationIdAccessor));
        Assert.Equal(typeof(CorrelationIdAccessor), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void ItRegistersTheCrmLogEnricherAndTheHttpContextAccessor()
    {
        var services = new ServiceCollection();

        services.AddCrmObservability(Configuration(), Environment());

        Assert.Contains(services, d => d.ServiceType == typeof(ILogEventEnricher));
        Assert.Contains(services, d => d.ServiceType == typeof(IHttpContextAccessor));
    }

    [Fact]
    public void ItRegistersTheOpenTelemetryHostedServices()
    {
        var services = new ServiceCollection();

        services.AddCrmObservability(Configuration(), Environment());

        Assert.Contains(services, d => d.ServiceType == typeof(IHostedService));
    }

    [Fact]
    public void ItReturnsTheCollectionForChaining()
    {
        var services = new ServiceCollection();

        Assert.Same(services, services.AddCrmObservability(Configuration(), Environment()));
    }

    [Fact]
    public void AnOtlpEndpointIsAcceptedAtRegistrationWithoutStartingAnything()
    {
        var services = new ServiceCollection();

        services.AddCrmObservability(Configuration(("OpenTelemetry:OtlpEndpoint", "http://localhost:4317")), Environment());

        Assert.Contains(services, d => d.ServiceType == typeof(ICorrelationIdAccessor));
    }

    [Fact]
    public void NullArgumentsAreRefused()
    {
        var services = new ServiceCollection();
        Func<IServiceCollection, IConfiguration, IHostEnvironment, IServiceCollection> method = ObservabilityServiceCollectionExtensions.AddCrmObservability;

        NullArguments.AssertThrowsArgumentNull(method, services, null, Environment());
        NullArguments.AssertThrowsArgumentNull(method, services, Configuration(), null);
    }
}
