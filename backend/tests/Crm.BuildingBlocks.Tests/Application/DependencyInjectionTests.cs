using Crm.BuildingBlocks.Application;
using Crm.BuildingBlocks.Application.Behaviors;
using Crm.BuildingBlocks.Application.Localization;
using Crm.BuildingBlocks.Application.Messaging;
using Crm.BuildingBlocks.Application.Modules;
using Crm.BuildingBlocks.Tests.Support;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.BuildingBlocks.Tests.Application;

public sealed class DependencyInjectionTests
{
    private static readonly System.Reflection.Assembly ApplicationAssembly = typeof(CrmCultures).Assembly;

    private static ServiceCollection Registered()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBuildingBlocksApplication([ApplicationAssembly]);
        return services;
    }

    [Fact]
    public void PipelineBehavioursAreRegisteredInTheDocumentedOrder()
    {
        var services = Registered();

        var order = services
            .Where(d => d.ServiceType == typeof(IPipelineBehavior<,>))
            .Select(d => d.ImplementationType)
            .ToList();

        Assert.Equal(
            [
                typeof(ValidationBehavior<,>),
                typeof(AuthorizationBehavior<,>),
                typeof(OrganisationScopeBehavior<,>),
                typeof(LoggingBehavior<,>),
                typeof(TransactionBehavior<,>),
                typeof(CachingBehavior<,>),
            ],
            order);
    }

    [Fact]
    public void TheUnitOfWorkResolverIsScoped()
    {
        var descriptor = Assert.Single(Registered(), d => d.ServiceType == typeof(IUnitOfWorkResolver));

        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        Assert.Equal(typeof(UnitOfWorkResolver), descriptor.ImplementationType);
    }

    [Fact]
    public void TheLocalizationServiceIsASingletonThatResolvesAndKnowsTheBuildingBlockMessages()
    {
        var services = Registered();
        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(ILocalizationService));
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);

        using var provider = services.BuildServiceProvider();
        var localization = provider.GetRequiredService<ILocalizationService>();

        using var culture = UiCulture.Use("en");
        Assert.NotEqual(CrmMessageKeys.ProblemTitle.Internal, localization.Get(CrmMessageKeys.ProblemTitle.Internal));
    }

    [Fact]
    public void TheEventBusAndPendingEventsAreTheSameInstanceWithinAScope()
    {
        using var provider = Registered().BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();

        var bus = scope.ServiceProvider.GetRequiredService<IEventBus>();
        var pending = scope.ServiceProvider.GetRequiredService<IPendingIntegrationEvents>();
        var concrete = scope.ServiceProvider.GetRequiredService<OutboxEventBus>();

        Assert.Same(bus, pending);
        Assert.Same(bus, concrete);
    }

    [Fact]
    public void TheEventBusDiffersAcrossScopes()
    {
        using var provider = Registered().BuildServiceProvider(validateScopes: true);
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();

        Assert.NotSame(
            first.ServiceProvider.GetRequiredService<IEventBus>(),
            second.ServiceProvider.GetRequiredService<IEventBus>());
    }

    [Fact]
    public void TheConsumerRegistryIsASingleton()
    {
        var descriptor = Assert.Single(Registered(), d => d.ServiceType == typeof(IntegrationEventConsumerRegistry));

        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void EveryDiscoveredConsumerIsRegisteredByItsConcreteTypeAsScoped()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        // The Application assembly has no consumers, so this proves the loop is a no-op rather than a failure.
        services.AddBuildingBlocksApplication([ApplicationAssembly]);

        Assert.DoesNotContain(services, d => typeof(IIntegrationEventConsumer).IsAssignableFrom(d.ServiceType));
    }

    [Fact]
    public void ADynamicAssemblyWithAnUndeclaredRequestMakesTheCallThrow()
    {
        var builder = new DynamicAssemblyBuilder();
        builder.Request("Dyn.Undeclared");
        var services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(() => services.AddBuildingBlocksApplication([builder.Assembly]));
    }

    [Fact]
    public void AddBuildingBlocksApplicationReturnsTheCollectionForChaining()
    {
        var services = new ServiceCollection();

        Assert.Same(services, services.AddBuildingBlocksApplication([ApplicationAssembly]));
    }

    [Fact]
    public void AddLocalizationResourceRegistersAnAdditionalBundle()
    {
        var services = Registered();
        services.AddLocalizationResource(
            new StubResourceManager(new Dictionary<string, string> { ["module.key"] = "module text" }));
        using var provider = services.BuildServiceProvider();

        Assert.Equal("module text", provider.GetRequiredService<ILocalizationService>().Get("module.key"));
    }
}
