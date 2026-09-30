using System.Diagnostics;
using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Exceptions;
using Crm.BuildingBlocks.Infrastructure.Outbox;
using Crm.SharedKernel;
using Crm.SharedKernel.Scoping;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;

namespace Crm.BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Base for every module's DbContext - one per module, never one for the whole application.
/// Each module owns its own SQL Server schema (<see cref="Schema"/>) in the shared <c>Crm</c>
/// database, its own migrations, and its own migrations-history table inside that schema, so a
/// module's tables can be moved to another database later without untangling anyone else's.
/// </summary>
/// <remarks>
/// A module's DbContext maps that module's aggregates only. It never maps, joins, or holds a
/// foreign key to another module's table - that other module is reached through its Contracts.
/// </remarks>
public abstract partial class ModuleDbContext(DbContextOptions options, ICurrentUserService currentUser) : DbContext(options), IUnitOfWork
{
    public const string SoftDeleteFilterName = "SoftDelete";

    private IDbContextTransaction? _currentTransaction;

    /// <summary>The module's SQL Server schema: its module name in lower case (e.g. "tickets").</summary>
    protected abstract string Schema { get; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        // The only registration path - never an inline modelBuilder.Entity<T>() and never a data
        // annotation (CLAUDE.md, "Backend", Persistence). Scans the module's own Infrastructure
        // assembly, which is why a module has exactly one DbContext.
        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);

        // Every module gets its own outbox, in its own schema (CLAUDE.md, modular monolith). Applied here, not
        // by assembly scan - these configurations live in this building-block assembly, not the module's.
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
        modelBuilder.ApplyConfiguration(new OutboxMessageConsumerConfiguration());

        modelBuilder.ApplySoftDeleteQueryFilter();
        modelBuilder.ApplyUtcDateTimeOffsetConverter();
        ApplyOrganisationScopeFilters(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }

    /// <summary>Translates EF's DbUpdateConcurrencyException into the Application-owned
    /// ConcurrencyConflictException, so TransactionBehavior can catch a concurrency conflict
    /// without an Application project referencing EF Core.</summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            EnsureWritesInScope();
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException(ex);
        }
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        _currentTransaction = await Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_currentTransaction is not null)
            {
                await _currentTransaction.CommitAsync(cancellationToken);
            }
        }
        finally
        {
            await DisposeCurrentTransactionAsync();
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_currentTransaction is not null)
            {
                await _currentTransaction.RollbackAsync(cancellationToken);
            }
        }
        finally
        {
            await DisposeCurrentTransactionAsync();
        }
    }

    public IReadOnlyCollection<IDomainEvent> GetAndClearDomainEvents()
    {
        List<IDomainEvent> domainEvents = ChangeTracker.Entries<IHasDomainEvents>()
            .SelectMany(e => e.Entity.DomainEvents)
            .ToList();

        foreach (EntityEntry<IHasDomainEvents> entry in ChangeTracker.Entries<IHasDomainEvents>())
        {
            entry.Entity.ClearDomainEvents();
        }

        return domainEvents;
    }

    public void AddToOutbox(IReadOnlyCollection<IIntegrationEvent> integrationEvents, string? correlationId)
    {
        ArgumentNullException.ThrowIfNull(integrationEvents);

        string? traceParent = Activity.Current?.Id;
        Set<OutboxMessage>().AddRange(integrationEvents.Select(e => OutboxMessage.From(e, correlationId, traceParent)));
    }

    internal async Task<IReadOnlyList<OutboxMessage>> GetDueOutboxMessagesAsync(
        DateTimeOffset nowUtc, int maxAttempts, int batchSize, CancellationToken cancellationToken) =>
        await Set<OutboxMessage>()
            .Where(m => m.ProcessedAtUtc == null
                        && m.Attempts < maxAttempts
                        && (m.NextAttemptAtUtc == null || m.NextAttemptAtUtc <= nowUtc))
            .OrderBy(m => m.OccurredOnUtc)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

    internal Task<bool> HasConsumedAsync(Guid messageId, string handler, CancellationToken cancellationToken) =>
        Set<OutboxMessageConsumer>().AnyAsync(c => c.MessageId == messageId && c.Handler == handler, cancellationToken);

    internal void RecordConsumer(Guid messageId, string handler, DateTimeOffset nowUtc) =>
        Set<OutboxMessageConsumer>().Add(new OutboxMessageConsumer(messageId, handler, nowUtc));

    private async Task DisposeCurrentTransactionAsync()
    {
        if (_currentTransaction is not null)
        {
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }
    }
}
