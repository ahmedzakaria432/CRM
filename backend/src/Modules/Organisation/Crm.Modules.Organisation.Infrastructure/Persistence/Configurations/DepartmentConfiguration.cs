using Crm.Modules.Organisation.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Modules.Organisation.Infrastructure.Persistence.Configurations;

public sealed class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.ToTable("Departments");
        builder.HasKey(x => x.Id);
        // Id (DepartmentId), TenantId, and BranchId are converted by the ModuleDbContext-wide convention (B4).
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Code)
            .HasConversion(c => c.Value, v => OrganisationCode.Create(v))
            .HasColumnName("Code")
            .HasMaxLength(20)
            .IsRequired();

        builder.OwnsOne(x => x.Name, name =>
        {
            name.Property(p => p.Arabic).HasColumnName("NameAr").HasMaxLength(200).IsRequired();
            name.Property(p => p.English).HasColumnName("NameEn").HasMaxLength(200).IsRequired();
        });

        builder.Property(x => x.RowVersion).IsRowVersion();

        // No FK to Branches (CLAUDE.md: no cross-aggregate FKs within a module's own schema either - ids only).
        builder.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.BranchId });
    }
}
