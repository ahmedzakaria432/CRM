using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Messaging;
using Crm.BuildingBlocks.Application.Modules;
using Crm.BuildingBlocks.Infrastructure.Correlation;
using Crm.BuildingBlocks.Infrastructure.Modules;
using Crm.BuildingBlocks.Infrastructure.Outbox;
using Crm.BuildingBlocks.Tests.Support.Persistence;
using Crm.SharedKernel;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Crm.BuildingBlocks.Tests.Support;

public sealed class AdjustableClock : IDateTime
{
    private readonly object _gate = new();
    private DateTimeOffset _now = Clock.Now;

    public DateTimeOffset UtcNow
    {
        get
        {
            lock (_gate)
            {
                return _now;
            }
        }
    }

    public void Advance(TimeSpan by)
    {
        lock (_gate)
        {
            _now += by;
        }
    }
}

/// <summary>Stands in for a module whose context was never registered, so its dispatch cycle fails.</summary>
public sealed class BrokenDbContextMarker;

/// <summary>An <see cref="OutboxDispatcher"/> wired over one InMemory store with the consumer fixtures of
/// <c>TestEvents</c>. The dispatcher is started for real and the store is polled with a bounded wait; InMemory has
/// no transactions, so a rollback is never asserted.</summary>
public sealed class OutboxHarness : IAsyncDisposable
{
    private static readonly System.Reflection.Assembly TestAssembly = typeof(SampleConsumer).Assembly;

    private readonly ServiceProvider _provider;

    public OutboxHarness(int maxAttempts = 5, int batchSize = 20, TimeSpan? pollingInterval = null, bool withBrokenModule = false)
    {
        Database = TestDbContext.NewDatabaseName();
        MaxAttempts = maxAttempts;
        var user = CurrentUsers.Unrestricted();
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        var registry = IntegrationEventConsumerRegistry.Scan([TestAssembly]);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Probe);
        services.AddSingleton(user);
        services.AddSingleton<IDateTime>(Time);
        services.AddSingleton(DomainEvents);
        services.AddSingleton(httpContextAccessor);
        services.AddSingleton<ICorrelationIdAccessor, CorrelationIdAccessor>();
        services.AddSingleton(registry);
        services.AddScoped(_ => TestDbContext.Create(Database, user, Time));
        services.AddScoped<ITestUnitOfWork>(sp => sp.GetRequiredService<TestDbContext>());
        services.AddSingleton(new ModuleDatabase("Test", "test", typeof(TestDbContext)));
        if (withBrokenModule)
        {
            services.AddSingleton(new ModuleDatabase("Broken", "broken", typeof(BrokenDbContextMarker)));
        }

        services.AddSingleton(new ModuleUnitOfWork("Test", TestAssembly, typeof(ITestUnitOfWork)));
        services.AddScoped<IUnitOfWorkResolver, UnitOfWorkResolver>();
        services.AddScoped<OutboxEventBus>();
        services.AddScoped<IEventBus>(sp => sp.GetRequiredService<OutboxEventBus>());
        services.AddScoped<IPendingIntegrationEvents>(sp => sp.GetRequiredService<OutboxEventBus>());
        services.AddScoped<IDomainEventDispatcher>(_ => DomainEvents);
        foreach (Type handler in registry.HandlerTypes)
        {
            services.AddScoped(handler);
        }

        _provider = services.BuildServiceProvider();
        Dispatcher = new OutboxDispatcher(
            _provider.GetRequiredService<IServiceScopeFactory>(),
            registry,
            new OutboxSettings(pollingInterval ?? TimeSpan.FromMilliseconds(10), batchSize, maxAttempts),
            Time,
            _provider.GetRequiredService<ICorrelationIdAccessor>(),
            Logger);
    }

    public string Database { get; }

    public int MaxAttempts { get; }

    public ConsumerProbe Probe { get; } = new();

    public AdjustableClock Time { get; } = new();

    public RecordingLogger<OutboxDispatcher> Logger { get; } = new();

    public IDomainEventDispatcher DomainEvents { get; } = Substitute.For<IDomainEventDispatcher>();

    public OutboxDispatcher Dispatcher { get; }

    public static OutboxMessage Message(IIntegrationEvent integrationEvent, string? correlationId = "corr-1") =>
        OutboxMessage.From(integrationEvent, correlationId, null);

    public TestDbContext Store() => TestDbContext.Store(Database);

    public async Task SeedAsync(params OutboxMessage[] messages)
    {
        await using var store = Store();
        store.Set<OutboxMessage>().AddRange(messages);
        await store.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async Task SeedConsumerAsync(Guid messageId, string handler)
    {
        await using var store = Store();
        store.Set<OutboxMessageConsumer>().Add(new OutboxMessageConsumer(messageId, handler, Time.UtcNow));
        await store.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async Task<OutboxMessage> ReadAsync(Guid id)
    {
        await using var store = Store();
        return await store.Set<OutboxMessage>().AsNoTracking().SingleAsync(m => m.Id == id, TestContext.Current.CancellationToken);
    }

    public async Task<List<OutboxMessage>> ReadAllAsync()
    {
        await using var store = Store();
        return await store.Set<OutboxMessage>().AsNoTracking().ToListAsync(TestContext.Current.CancellationToken);
    }

    public async Task<List<OutboxMessageConsumer>> ConsumersAsync()
    {
        await using var store = Store();
        return await store.Set<OutboxMessageConsumer>().AsNoTracking().ToListAsync(TestContext.Current.CancellationToken);
    }

    public async Task<Running> StartAsync()
    {
        await Dispatcher.StartAsync(TestContext.Current.CancellationToken);
        return new Running(this);
    }

    public ValueTask DisposeAsync()
    {
        Dispatcher.Dispose();
        return _provider.DisposeAsync();
    }

    public sealed class Running(OutboxHarness harness) : IAsyncDisposable
    {
        /// <summary>Polls until <paramref name="condition"/> holds, failing the test after five seconds.</summary>
        public async Task UntilAsync(Func<OutboxHarness, Task<bool>> condition)
        {
            ArgumentNullException.ThrowIfNull(condition);

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                while (!await condition(harness))
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(10), timeout.Token);
                }
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested)
            {
                Assert.Fail("The expected outbox state was not reached within five seconds.");
            }
        }

        public async Task UntilProcessedAsync(Guid messageId) =>
            await UntilAsync(async h => (await h.ReadAsync(messageId)).ProcessedAtUtc is not null);

        public async Task UntilAttemptsAsync(Guid messageId, int attempts) =>
            await UntilAsync(async h => (await h.ReadAsync(messageId)).Attempts >= attempts);

        public async Task UntilErrorsLoggedAsync(int count) =>
            await UntilAsync(h => Task.FromResult(h.Logger.Entries.Count(e => e.Level == LogLevel.Error) >= count));

        public async ValueTask DisposeAsync() => await harness.Dispatcher.StopAsync(CancellationToken.None);
    }
}
