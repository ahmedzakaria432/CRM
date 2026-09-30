using Crm.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Modules.Identity.Infrastructure.Persistence.Configurations;

public sealed class PortalSessionConfiguration : IEntityTypeConfiguration<PortalSession>
{
    public void Configure(EntityTypeBuilder<PortalSession> builder)
    {
        builder.ToTable("PortalSessions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasConversion(id => id.Value, value => new PortalSessionId(value))
            .ValueGeneratedNever();

        builder.Property(x => x.PortalAccountId)
            .HasConversion(id => id.Value, value => new PortalAccountId(value));

        builder.Property(x => x.RevocationReason).HasConversion<string>().HasMaxLength(30);

        builder.OwnsMany(x => x.Tokens, tokens =>
        {
            tokens.ToTable("PortalRefreshTokens");
            tokens.WithOwner().HasForeignKey("SessionId");
            tokens.HasKey(t => t.Id);
            // Client-generated key: without this EF treats a token added to a tracked session as an existing row (UPDATE, 0 rows).
            tokens.Property(t => t.Id).ValueGeneratedNever();
            tokens.Property(t => t.TokenHash).HasMaxLength(64).IsRequired();
            tokens.HasIndex(t => t.TokenHash).IsUnique();
        });

        // Lets EF materialise into the private List<RefreshToken> backing field.
        builder.Navigation(x => x.Tokens).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasIndex(x => x.PortalAccountId);
    }
}
