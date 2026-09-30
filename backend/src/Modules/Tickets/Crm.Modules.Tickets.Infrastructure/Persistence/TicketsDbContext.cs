using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Exceptions;
using Crm.BuildingBlocks.Infrastructure.Persistence;
using Crm.Modules.Tickets.Application;
using Crm.Modules.Tickets.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Crm.Modules.Tickets.Infrastructure.Persistence;

public sealed class TicketsDbContext(DbContextOptions<TicketsDbContext> options, ICurrentUserService currentUser)
    : ModuleDbContext(options, currentUser), ITicketsUnitOfWork
{
    public const string SchemaName = "tickets";

    /// <summary>tickets.TicketNumbers - SqlTicketNumberGenerator SQL names it literally.</summary>
    public const string TicketNumberSequence = "TicketNumbers";

    protected override string Schema => SchemaName;

    // Aggregate roots only. TicketActivity, TicketMessage and TicketNote have no DbSet - reachable only through Ticket.
    public DbSet<Ticket> Tickets => Set<Ticket>();

    public DbSet<Category> Categories => Set<Category>();

    /// <summary>Ticket owns three collections (activities, messages, notes) that EF loads with every ticket. One SQL
    /// statement would multiply them (a cartesian product of message bodies); split queries load each once (Story 18).
    /// Tickets context only - no other module is affected.</summary>
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);
        base.OnConfiguring(optionsBuilder);
        new SqlServerDbContextOptionsBuilder(optionsBuilder).UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
    }

    /// <summary>Two simultaneous appends to one ticket both take the same Sequence. EF sends the child INSERT before the
    /// root UPDATE that carries the rowversion check, so the loser hits the unique (TicketId, Sequence) index first.
    /// That clash IS a concurrency conflict (409 Persistence.ConcurrencyConflict, the client resends), never a 500
    /// (Story 18, R9).</summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsSequenceCollision(ex))
        {
            throw new ConcurrencyConflictException(ex);
        }
    }

    private static bool IsSequenceCollision(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 } sql
        && sql.Message.Contains("_TicketId_Sequence", StringComparison.Ordinal);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // The one model element that is not an entity type, so no IEntityTypeConfiguration can declare it.
        modelBuilder.HasSequence<long>(TicketNumberSequence, SchemaName).StartsAt(1).IncrementsBy(1);
    }
}
