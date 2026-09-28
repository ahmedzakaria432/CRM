// CANONICAL SLICE - copy this shape for every new use case (CLAUDE.md, "Backend", CQRS).
// DELETE this whole Sample surface in the story that lands the first real use case (Story 11).

using Crm.Modules.Sample.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Modules.Sample.Infrastructure.Persistence.Configurations;

public sealed class SampleAggregateConfiguration : IEntityTypeConfiguration<SampleAggregate>
{
    public void Configure(EntityTypeBuilder<SampleAggregate> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasConversion(id => id.Value, value => new SampleId(value))
            .ValueGeneratedNever();

        // SampleName's constructor parameter is named "value" to match this mapped property name -
        // EF Core requires that match (case-insensitive) to materialise an owned value object, or
        // model build fails with "No suitable constructor was found". Applies to every later value
        // object (EmailAddress, PhoneNumber, Money, ...).
        builder.OwnsOne(x => x.Name, name =>
            name.Property(p => p.Value).HasColumnName("Name").HasMaxLength(200).IsRequired());

        builder.OwnsMany(x => x.Lines, lines =>
        {
            lines.ToTable("SampleLines");
            lines.WithOwner().HasForeignKey("SampleId");
            lines.HasKey(l => l.Id);
        });

        // Lets EF materialise into the private List<SampleLine> backing field with no public
        // setter and no public parameterless constructor on the aggregate.
        builder.Navigation(x => x.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Property(x => x.RowVersion).IsRowVersion();

        // Every query carries WHERE IsDeleted = 0 from the global soft-delete filter; without this
        // index every read is a table scan from Story 11 onward.
        builder.HasIndex(x => x.IsDeleted);
    }
}
