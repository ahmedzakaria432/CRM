using Crm.Modules.Organisation.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Modules.Organisation.Infrastructure.Persistence.Configurations;

public sealed class BrandLogoConfiguration : IEntityTypeConfiguration<BrandLogo>
{
    public void Configure(EntityTypeBuilder<BrandLogo> builder)
    {
        builder.ToTable("BrandLogos");
        builder.HasKey(x => x.Id);

        // TenantId is converted by the ModuleDbContext-wide convention; BrandLogoId is this aggregate's own identity.
        builder.Property(x => x.Id)
            .HasConversion(id => id.Value, value => new BrandLogoId(value))
            .ValueGeneratedNever();

        builder.Property(x => x.ContentType).HasConversion<string>().HasMaxLength(10);
        builder.Property(x => x.Content).IsRequired();
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.Ignore(x => x.MediaType);

        builder.HasIndex(x => x.TenantId);
    }
}
