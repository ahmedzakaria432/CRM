using Crm.Modules.Organisation.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Modules.Organisation.Infrastructure.Persistence.Configurations;

public sealed class MembershipConfiguration : IEntityTypeConfiguration<Membership>
{
    public void Configure(EntityTypeBuilder<Membership> builder)
    {
        builder.ToTable("Memberships");
        builder.HasKey(x => x.Id);

        // TenantId, DepartmentId, and BranchId are converted by the ModuleDbContext-wide convention (B4) -
        // they are scope columns, mapped for every module. MembershipId is this aggregate's own identity, not
        // one of the three scope types, so (like SampleId) it needs its own conversion here.
        builder.Property(x => x.Id)
            .HasConversion(id => id.Value, value => new MembershipId(value))
            .ValueGeneratedNever();

        builder.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);

        builder.Property(x => x.RowVersion).IsRowVersion();

        // Filtered so a soft-deleted membership never blocks re-adding the same (user, department) pair -
        // OrganisationSeeder relies on this to restore a removed seed membership without an id clash.
        builder.HasIndex(x => new { x.UserId, x.DepartmentId }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(x => new { x.TenantId, x.DepartmentId });
        builder.HasIndex(x => x.IsDeleted);
    }
}
