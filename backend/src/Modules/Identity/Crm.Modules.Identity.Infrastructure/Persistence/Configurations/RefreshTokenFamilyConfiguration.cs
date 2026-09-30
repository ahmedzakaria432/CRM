using Crm.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Modules.Identity.Infrastructure.Persistence.Configurations;

public sealed class RefreshTokenFamilyConfiguration : IEntityTypeConfiguration<RefreshTokenFamily>
{
    public void Configure(EntityTypeBuilder<RefreshTokenFamily> builder)
    {
        builder.ToTable("RefreshTokenFamilies");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasConversion(id => id.Value, value => new RefreshTokenFamilyId(value))
            .ValueGeneratedNever();

        builder.Property(x => x.UserId)
            .HasConversion(id => id.Value, value => new UserId(value));

        builder.Property(x => x.RevocationReason).HasConversion<string>().HasMaxLength(30);

        builder.OwnsMany(x => x.Tokens, tokens =>
        {
            tokens.ToTable("RefreshTokens");
            tokens.WithOwner().HasForeignKey("FamilyId");
            tokens.HasKey(t => t.Id);
            // Client-generated key: without this EF treats a token added to a tracked family as an existing row (UPDATE, 0 rows).
            tokens.Property(t => t.Id).ValueGeneratedNever();
            tokens.Property(t => t.TokenHash).HasMaxLength(64).IsRequired();
            tokens.HasIndex(t => t.TokenHash).IsUnique();
        });

        // Lets EF materialise into the private List<RefreshToken> backing field.
        builder.Navigation(x => x.Tokens).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasIndex(x => x.UserId);
    }
}
