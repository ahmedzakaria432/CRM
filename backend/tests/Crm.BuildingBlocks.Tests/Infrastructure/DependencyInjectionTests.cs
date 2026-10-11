using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Caching;
using Crm.BuildingBlocks.Infrastructure;
using Crm.BuildingBlocks.Infrastructure.Caching;
using Crm.BuildingBlocks.Infrastructure.Correlation;
using Crm.BuildingBlocks.Infrastructure.Identity;
using Crm.BuildingBlocks.Infrastructure.Outbox;
using Crm.BuildingBlocks.Infrastructure.Persistence;
using Crm.BuildingBlocks.Infrastructure.Persistence.Interceptors;
using Crm.BuildingBlocks.Infrastructure.Time;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Crm.BuildingBlocks.Tests.Infrastructure;

public sealed class DependencyInjectionTests
{
    private const string SqlConnection = "Server=localhost;Database=Crm;Integrated Security=true;TrustServerCertificate=true";

    private static IConfiguration Configuration(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();

    private static IHostEnvironment Environment(string name)
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(name);
        return environment;
    }

    private static ServiceCollection Register(string environment = "Production", params (string Key, string Value)[] values)
    {
        var services = new ServiceCollection();
        var all = new List<(string Key, string Value)> { ("ConnectionStrings:Crm", SqlConnection) };
        all.AddRange(values);
        services.AddBuildingBlocksInfrastructure(Configuration([.. all]), Environment(environment));
        return services;
    }

    private static ServiceDescriptor Single<T>(ServiceCollection services) =>
        Assert.Single(services, d => d.ServiceType == typeof(T));

    [Fact]
    public void TheClockIsTheSystemClockAsASingleton()
    {
        var descriptor = Single<IDateTime>(Register());

        Assert.Equal(typeof(SystemDateTime), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Theory]
    [InlineData("Development", true)]
    [InlineData("Production", false)]
    [InlineData("Staging", false)]
    public void SensitiveDataLoggingIsOnOnlyInDevelopment(string environment, bool expected)
    {
        var descriptor = Single<PersistenceSettings>(Register(environment));

        var settings = Assert.IsType<PersistenceSettings>(descriptor.ImplementationInstance);
        Assert.Equal(expected, settings.EnableSensitiveDataLogging);
    }

    [Fact]
    public void TheCurrentUserIsScopedAndHttpContextBacked()
    {
        var descriptor = Single<ICurrentUserService>(Register());

        Assert.Equal(typeof(CurrentUserService), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void TheAuditingInterceptorAndDomainEventDispatcherAreScoped()
    {
        var services = Register();

        Assert.Equal(ServiceLifetime.Scoped, Single<AuditingSaveChangesInterceptor>(services).Lifetime);
        var dispatcher = Single<IDomainEventDispatcher>(services);
        Assert.Equal(typeof(DomainEventDispatcher), dispatcher.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, dispatcher.Lifetime);
    }

    [Fact]
    public void OutboxSettingsComeFromTheOutboxSectionWhenPresent()
    {
        var descriptor = Single<OutboxSettings>(Register(
            "Production",
            ("Outbox:PollingInterval", "00:00:09"),
            ("Outbox:BatchSize", "7"),
            ("Outbox:MaxAttempts", "3")));

        Assert.Equal(new OutboxSettings(TimeSpan.FromSeconds(9), 7, 3), descriptor.ImplementationInstance);
    }

    [Fact]
    public void OutboxSettingsFallBackToTheDefault()
    {
        var descriptor = Single<OutboxSettings>(Register());

        Assert.Same(OutboxSettings.Default, descriptor.ImplementationInstance);
    }

    [Fact]
    public void CachingSettingsComeFromTheCachingSectionWhenPresent()
    {
        var descriptor = Single<CachingSettings>(Register("Production", ("Caching:DefaultExpiration", "00:10:00")));

        Assert.Equal(new CachingSettings(TimeSpan.FromMinutes(10)), descriptor.ImplementationInstance);
    }

    [Fact]
    public void CachingSettingsFallBackToTheDefault()
    {
        var descriptor = Single<CachingSettings>(Register());

        Assert.Same(CachingSettings.Default, descriptor.ImplementationInstance);
    }

    [Fact]
    public void TheCacheServiceIsTheDistributedCacheService()
    {
        var descriptor = Single<ICacheService>(Register());

        Assert.Equal(typeof(DistributedCacheService), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void TheOutboxDispatcherIsAHostedService()
    {
        var services = Register();

        Assert.Contains(
            services,
            d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(OutboxDispatcher));
    }

    [Fact]
    public void TheOutboundCorrelationHandlerIsTransient()
    {
        var descriptor = Single<CorrelationIdDelegatingHandler>(Register());

        Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
    }

    [Fact]
    public void AMissingCrmConnectionStringFailsAtRegistration()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddBuildingBlocksInfrastructure(Configuration(), Environment("Production")));

        Assert.Contains("'Crm'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void WithoutARedisStringTheInProcessCacheIsUsed()
    {
        var configuration = Configuration(("ConnectionStrings:Crm", SqlConnection));
        var services = Register();

        Assert.True(DependencyInjection.UsesInProcessCache(configuration));
        Assert.Contains(services, d => d.ServiceType == typeof(Microsoft.Extensions.Caching.Distributed.IDistributedCache));
    }

    [Fact]
    public void WithARedisStringTheInProcessCacheIsNotUsed()
    {
        var configuration = Configuration(
            ("ConnectionStrings:Crm", SqlConnection), ("ConnectionStrings:Redis", "localhost:6379"));

        Assert.False(DependencyInjection.UsesInProcessCache(configuration));
    }

    [Fact]
    public void ARedisStringSwitchesTheDistributedCacheRegistration()
    {
        var inProcess = Register();
        var redis = Register("Production", ("ConnectionStrings:Redis", "localhost:6379"));

        var inProcessCache = Single<Microsoft.Extensions.Caching.Distributed.IDistributedCache>(inProcess);
        var redisCache = Single<Microsoft.Extensions.Caching.Distributed.IDistributedCache>(redis);

        Assert.NotEqual(inProcessCache.ImplementationType, redisCache.ImplementationType);
    }
}
