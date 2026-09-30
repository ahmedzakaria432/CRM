using Crm.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Modules.Identity.Infrastructure.Persistence.Configurations;

public sealed class PortalAccountConfiguration : IEntityTypeConfiguration<PortalAccount>
{
    public void Configure(EntityTypeBuilder<PortalAccount> builder)
    {
        builder.ToTable("PortalAccounts");
        builder.HasKey(x => x.Id);

        // TenantId is converted by the ModuleDbContext convention - never configure it here.
        builder.Property(x => x.Id)
            .HasConversion(id => id.Value, value => new PortalAccountId(value))
            .ValueGeneratedNever();

        builder.OwnsOne(x => x.Email, email =>
        {
            email.Property(p => p.Value).HasColumnName("Email").HasMaxLength(EmailAddress.MaxLength).IsRequired();
            email.HasIndex(p => p.Value).IsUnique();
        });

        builder.OwnsOne(x => x.DisplayName, name =>
            name.Property(p => p.Value).HasColumnName("DisplayName").HasMaxLength(DisplayName.MaxLength).IsRequired());

        builder.Property(x => x.PreferredLocale)
            .HasConversion(l => l.Value, v => PreferredLocale.Create(v))
            .HasColumnName("PreferredLocale")
            .HasMaxLength(2)
            .IsRequired();

        builder.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();

        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasIndex(x => x.ContactId).IsUnique();
        builder.HasIndex(x => x.CustomerId);
        builder.HasIndex(x => x.TenantId);
    }
}
