using Crm.Modules.Tickets.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Modules.Tickets.Infrastructure.Persistence.Configurations;

public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");
        builder.HasKey(x => x.Id);

        // TenantId/DepartmentId: ModuleDbContext convention - never configure them here.
        builder.Property(x => x.Id)
            .HasConversion(id => id.Value, value => new CategoryId(value))
            .ValueGeneratedNever();

        builder.OwnsOne(x => x.Name, name =>
        {
            name.Property(p => p.Arabic).HasColumnName("NameAr").HasMaxLength(LocalizedText.MaxLength).IsRequired();
            name.Property(p => p.English).HasColumnName("NameEn").HasMaxLength(LocalizedText.MaxLength).IsRequired();
        });

        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasIndex(x => new { x.TenantId, x.DepartmentId });
    }
}
