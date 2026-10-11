using System.Diagnostics;
using Crm.BuildingBlocks.Application.Exceptions;
using Crm.BuildingBlocks.Infrastructure.Outbox;
using Crm.BuildingBlocks.Tests.Support;
using Crm.BuildingBlocks.Tests.Support.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Crm.BuildingBlocks.Tests.Infrastructure.Persistence;

public sealed class ModuleDbContextUnitOfWorkTests
{
    private static SampleIntegrationEvent Event(string payload = "p") => new(Guid.NewGuid(), Clock.Now, payload);

    // --- transactions (R4) --------------------------------------------------------------------------------

    [Fact]
    public async Task BeginAndCommitCompleteUnderTheInMemoryProvider()
    {
        await using var context = TestDbContext.Store(TestDbContext.NewDatabaseName());

        await context.BeginTransactionAsync(TestContext.Current.CancellationToken);
        context.UnscopedRows.Add(UnscopedRow.Create(Guid.NewGuid(), "x"));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await context.CommitTransactionAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, await context.UnscopedRows.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task BeginAndRollbackComplete()
    {
        await using var context = TestDbContext.Store(TestDbContext.NewDatabaseName());

        await context.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await context.RollbackTransactionAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CommitAndRollbackWithoutATransactionAreNoOps()
    {
        await using var context = TestDbContext.Store(TestDbContext.NewDatabaseName());

        await context.CommitTransactionAsync(TestContext.Current.CancellationToken);
        await context.RollbackTransactionAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ATransactionCanBeOpenedAgainAfterItEnds()
    {
        await using var context = TestDbContext.Store(TestDbContext.NewDatabaseName());

        await context.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await context.CommitTransactionAsync(TestContext.Current.CancellationToken);
        await context.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await context.CommitTransactionAsync(TestContext.Current.CancellationToken);
    }

    // --- domain events ------------------------------------------------------------------------------------

    [Fact]
    public async Task GetAndClearDomainEventsHarvestsEveryTrackedAggregateAndClearsThem()
    {
        await using var context = TestDbContext.Store(TestDbContext.NewDatabaseName());
        var first = EventfulRow.Create(Guid.NewGuid());
        var second = EventfulRow.Create(Guid.NewGuid());
        first.Touch(Clock.Now);
        second.Touch(Clock.Now.AddMinutes(1));
        first.Touch(Clock.Now.AddMinutes(2));
        context.EventfulRows.AddRange(first, second);

        var events = context.GetAndClearDomainEvents();

        Assert.Equal(3, events.Count);
        Assert.Empty(first.DomainEvents);
        Assert.Empty(second.DomainEvents);
        Assert.Empty(context.GetAndClearDomainEvents());
    }

    [Fact]
    public async Task GetAndClearDomainEventsIsEmptyWhenNothingRaisedAnything()
    {
        await using var context = TestDbContext.Store(TestDbContext.NewDatabaseName());
        context.UnscopedRows.Add(UnscopedRow.Create(Guid.NewGuid(), "quiet"));

        Assert.Empty(context.GetAndClearDomainEvents());
    }

    // --- outbox -------------------------------------------------------------------------------------------

    [Fact]
    public async Task AddToOutboxStagesOneMessagePerEventAndSaveChangesPersistsThem()
    {
        var database = TestDbContext.NewDatabaseName();
        var first = Event("a");
        var second = Event("b");
        Activity.Current = null;

        await using (var context = TestDbContext.Store(database))
        {
            context.AddToOutbox([first, second], "corr-9");
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var store = TestDbContext.Store(database);
        var messages = await store.Set<OutboxMessage>().OrderBy(m => m.Content).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, messages.Count);
        Assert.Equal([first.Id, second.Id], messages.Select(m => m.Id));
        Assert.All(messages, m => Assert.Equal(typeof(SampleIntegrationEvent).FullName, m.Type));
        Assert.All(messages, m => Assert.Equal("corr-9", m.CorrelationId));
        Assert.All(messages, m => Assert.Null(m.TraceParent));
    }

    [Fact]
    public async Task AddToOutboxRecordsTheCurrentTraceParent()
    {
        await using var context = TestDbContext.Store(TestDbContext.NewDatabaseName());
        using var activity = new Activity("outbox-trace-test");
        activity.Start();

        context.AddToOutbox([Event()], null);
        activity.Stop();

        var staged = Assert.Single(context.ChangeTracker.Entries<OutboxMessage>());
        Assert.Equal(activity.Id, staged.Entity.TraceParent);
        Assert.Null(staged.Entity.CorrelationId);
    }

    [Fact]
    public async Task AddToOutboxRejectsANullList()
    {
        await using var context = TestDbContext.Store(TestDbContext.NewDatabaseName());
        Action<IReadOnlyCollection<Crm.SharedKernel.IIntegrationEvent>, string?> method = context.AddToOutbox;

        NullArguments.AssertThrowsArgumentNull(method, null, "corr");
    }

    // --- concurrency --------------------------------------------------------------------------------------

    [Fact]
    public async Task AConcurrencyFailureIsTranslatedAndKeepsTheOriginalAsInner()
    {
        var interceptor = new ThrowingInterceptor();
        await using var context = TestDbContext.Create(
            TestDbContext.NewDatabaseName(), CurrentUsers.Unrestricted(), null, interceptor);
        context.UnscopedRows.Add(UnscopedRow.Create(Guid.NewGuid(), "x"));

        var exception = await Assert.ThrowsAsync<ConcurrencyConflictException>(
            () => context.SaveChangesAsync(TestContext.Current.CancellationToken));

        Assert.Same(interceptor.Thrown, exception.InnerException);
    }

    // --- model --------------------------------------------------------------------------------------------

    [Fact]
    public async Task TheModelHasBothOutboxTablesInTheModulesSchema()
    {
        await using var context = TestDbContext.Store(TestDbContext.NewDatabaseName());

        var messages = context.Model.FindEntityType(typeof(OutboxMessage));
        var consumers = context.Model.FindEntityType(typeof(OutboxMessageConsumer));

        Assert.NotNull(messages);
        Assert.NotNull(consumers);
        Assert.Equal("outbox_messages", messages.GetTableName());
        Assert.Equal("outbox_message_consumers", consumers.GetTableName());
        Assert.Equal("test", messages.GetSchema() ?? context.Model.GetDefaultSchema());
        Assert.Equal("test", consumers.GetSchema() ?? context.Model.GetDefaultSchema());
    }

    [Fact]
    public async Task ADateTimeOffsetRoundTripsAsUtc()
    {
        var database = TestDbContext.NewDatabaseName();
        var local = new DateTimeOffset(2026, 10, 11, 12, 0, 0, TimeSpan.FromHours(3));
        var id = Guid.NewGuid();
        await using (var context = TestDbContext.Store(database))
        {
            context.UnscopedRows.Add(UnscopedRow.Create(id, "tz", local));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var store = TestDbContext.Store(database);
        var read = (await store.UnscopedRows.SingleAsync(r => r.Id == id, TestContext.Current.CancellationToken)).SeenAtUtc;

        Assert.Equal(local, read);
        Assert.Equal(TimeSpan.Zero, read?.Offset);
    }
}
