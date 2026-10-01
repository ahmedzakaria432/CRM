using Crm.Modules.Organisation.Domain;
using Crm.Modules.Organisation.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Modules.Organisation.Infrastructure.Persistence.Configurations;

public sealed class OrganisationConfiguration : IEntityTypeConfiguration<Domain.Organisation>
{
    public void Configure(EntityTypeBuilder<Domain.Organisation> builder)
    {
        builder.ToTable("Organisations");
        builder.HasKey(x => x.Id);
        // Id (TenantId) is converted by the ModuleDbContext-wide convention - no per-property conversion here.
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

        // Story 58 (D1): the branding value objects as JSON; NULL = product defaults.
        builder.Property(x => x.Branding).HasConversion(new BrandingConverter()).HasColumnName("Branding").HasMaxLength(4000);
        builder.Property(x => x.PreviousBranding).HasConversion(new BrandingConverter()).HasColumnName("PreviousBranding").HasMaxLength(4000);
        builder.Property(x => x.CanRevertBranding).IsRequired();

        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasIndex(x => x.Code).IsUnique();
    }
}
