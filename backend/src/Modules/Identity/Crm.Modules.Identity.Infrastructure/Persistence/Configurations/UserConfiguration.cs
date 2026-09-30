using Crm.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Modules.Identity.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(x => x.Id);

        // UserId is not one of the scope types the ModuleDbContext convention converts, so it needs its own
        // conversion (like MembershipId). TenantId is converted by the convention - never configure it here.
        builder.Property(x => x.Id)
            .HasConversion(id => id.Value, value => new UserId(value))
            .ValueGeneratedNever();

        // OwnsOne (not HasConversion) so u.Email.Value.Contains(search) translates to SQL.
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

        builder.HasIndex(x => x.TenantId);
    }
}
