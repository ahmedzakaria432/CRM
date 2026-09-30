using Crm.Modules.Organisation.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Modules.Organisation.Infrastructure.Persistence.Configurations;

public sealed class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> builder)
    {
        builder.ToTable("Branches");
        builder.HasKey(x => x.Id);
        // Id (BranchId) and TenantId are converted by the ModuleDbContext-wide convention (B4) - never
        // configure a conversion for them here.
        builder.Property(x => x.Id).ValueGeneratedNever();

        // Code is HasConversion, not OwnsOne: a composite unique index cannot span an owner column
        // (TenantId) and an owned-type column, and HasIndex(x => new { x.TenantId, x.Code }) needs both
        // directly on the owner.
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

        builder.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
    }
}
