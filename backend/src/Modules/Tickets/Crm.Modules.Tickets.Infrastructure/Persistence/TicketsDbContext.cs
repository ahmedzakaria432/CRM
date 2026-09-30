using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Infrastructure.Persistence;
using Crm.Modules.Tickets.Application;
using Crm.Modules.Tickets.Domain;
using Microsoft.EntityFrameworkCore;

namespace Crm.Modules.Tickets.Infrastructure.Persistence;

public sealed class TicketsDbContext(DbContextOptions<TicketsDbContext> options, ICurrentUserService currentUser)
    : ModuleDbContext(options, currentUser), ITicketsUnitOfWork
{
    public const string SchemaName = "tickets";

    /// <summary>tickets.TicketNumbers - SqlTicketNumberGenerator SQL names it literally.</summary>
    public const string TicketNumberSequence = "TicketNumbers";

    protected override string Schema => SchemaName;

    // Aggregate roots only. TicketActivity has no DbSet - reachable only through Ticket.
    public DbSet<Ticket> Tickets => Set<Ticket>();

    public DbSet<Category> Categories => Set<Category>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // The one model element that is not an entity type, so no IEntityTypeConfiguration can declare it.
        modelBuilder.HasSequence<long>(TicketNumberSequence, SchemaName).StartsAt(1).IncrementsBy(1);
    }
}
