using Crm.Modules.Tickets.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Crm.Modules.Tickets.Infrastructure.Persistence.Configurations;

public sealed class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        builder.ToTable("Tickets");
        builder.HasKey(x => x.Id);

        // TenantId/BranchId/DepartmentId: ModuleDbContext convention - never configure them here.
        builder.Property(x => x.Id)
            .HasConversion(id => id.Value, value => new TicketId(value))
            .ValueGeneratedNever();

        // OwnsOne (not HasConversion) so t.Number.Value.Contains(term) translates. Unique globally - the sequence is global.
        builder.OwnsOne(x => x.Number, number =>
        {
            number.Property(p => p.Value).HasColumnName("Number").HasMaxLength(TicketNumber.MaxLength).IsRequired();
            number.HasIndex(p => p.Value).IsUnique();
        });

        builder.Property(x => x.Subject).HasMaxLength(Ticket.SubjectMaxLength).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(Ticket.DescriptionMaxLength).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Channel).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Priority).HasConversion<int>(); // sorts by rank

        // Nullable reference: a converter class (a lambda on a nullable reference is CS8602).
        builder.Property(x => x.CategoryId).HasConversion<CategoryIdConverter>();

        builder.OwnsMany(x => x.Activities, activities =>
        {
            activities.ToTable("TicketActivities");
            activities.WithOwner().HasForeignKey("TicketId");
            activities.HasKey(a => a.Id);

            // Client-generated key: without this EF treats an activity added to a tracked ticket as an existing row
            // (UPDATE, 0 rows).
            activities.Property(a => a.Id).ValueGeneratedNever();
            activities.Property(a => a.Kind).HasConversion<string>().HasMaxLength(30);
            activities.Property(a => a.From).HasMaxLength(TicketActivity.ValueMaxLength);
            activities.Property(a => a.To).HasMaxLength(TicketActivity.ValueMaxLength);
            activities.HasIndex("TicketId", nameof(TicketActivity.Sequence)).IsUnique();
        });
        builder.Navigation(x => x.Activities).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(x => x.Messages, messages =>
        {
            messages.ToTable("TicketMessages");
            messages.WithOwner().HasForeignKey("TicketId");
            messages.HasKey(m => m.Id);
            messages.Property(m => m.Id).ValueGeneratedNever(); // client-generated key: INSERT, not UPDATE
            messages.Property(m => m.Direction).HasConversion<string>().HasMaxLength(10);
            messages.Property(m => m.Body).HasMaxLength(Ticket.BodyMaxLength).IsRequired(); // nvarchar(max)
            messages.HasIndex("TicketId", nameof(TicketMessage.Sequence)).IsUnique();
        });
        builder.Navigation(x => x.Messages).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(x => x.Notes, notes =>
        {
            notes.ToTable("TicketNotes");
            notes.WithOwner().HasForeignKey("TicketId");
            notes.HasKey(n => n.Id);
            notes.Property(n => n.Id).ValueGeneratedNever();
            notes.Property(n => n.Body).HasMaxLength(Ticket.BodyMaxLength).IsRequired();
            notes.HasIndex("TicketId", nameof(TicketNote.Sequence)).IsUnique();
        });
        builder.Navigation(x => x.Notes).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasIndex(x => x.IsDeleted);
        builder.HasIndex(x => new { x.TenantId, x.DepartmentId, x.Status });
        builder.HasIndex(x => x.AssignedAgentId);
        builder.HasIndex(x => x.CustomerId);
        builder.HasIndex(x => x.CategoryId);

        // No FK to Categories and none to another module (CLAUDE.md: ids only).
    }

    private sealed class CategoryIdConverter() : ValueConverter<CategoryId, Guid>(v => v.Value, v => new CategoryId(v));
}
