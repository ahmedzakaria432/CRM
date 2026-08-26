using Crm.Application.Common.Abstractions;
using Crm.Application.Common.Exceptions;
using Crm.Domain.Common;
using Crm.Domain.Sample;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;

namespace Crm.Infrastructure.Persistence;

public sealed class CrmDbContext(DbContextOptions<CrmDbContext> options)
    : DbContext(options), IUnitOfWork
{
    public const string SoftDeleteFilterName = "SoftDelete";

    private IDbContextTransaction? _currentTransaction;

    // Aggregate roots only. A child entity never gets a DbSet - CLAUDE.md, "Backend", DDD rules.
    // CANONICAL SLICE - deleted alongside the rest of Crm.Domain/Sample in Story 11.
    public DbSet<SampleAggregate> Samples => Set<SampleAggregate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // The only registration path - never an inline modelBuilder.Entity<T>() and never a data
        // annotation (CLAUDE.md, "Backend", Persistence).
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CrmDbContext).Assembly);
        modelBuilder.ApplySoftDeleteQueryFilter();
        modelBuilder.ApplyUtcDateTimeOffsetConverter();

        base.OnModelCreating(modelBuilder);
    }

    /// <summary>Translates EF's DbUpdateConcurrencyException into the Application-owned
    /// ConcurrencyConflictException, so TransactionBehavior can catch a concurrency conflict
    /// without Crm.Application referencing EF Core.</summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
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

    private async Task DisposeCurrentTransactionAsync()
    {
        if (_currentTransaction is not null)
        {
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }
    }
}
