using Crm.Modules.Organisation.Contracts.DevSeed;
using Crm.Modules.Organisation.Domain;
using Crm.Modules.Organisation.Infrastructure.Persistence;
using Crm.Modules.Organisation.Tests.Support;
using Crm.SharedKernel.Scoping;
using Microsoft.EntityFrameworkCore;
using OrganisationAggregate = Crm.Modules.Organisation.Domain.Organisation;

namespace Crm.Modules.Organisation.Tests.Infrastructure;

public sealed class OrganisationSeederTests
{
    private readonly string _database = OrganisationTestContext.NewDatabaseName();

    private async Task SeedAsync()
    {
        await using OrganisationDbContext context = OrganisationTestContext.Store(_database);
        await new OrganisationSeeder(context).SeedAsync(Stubs.Ct);
    }

    private async Task<T> ReadAsync<T>(Func<OrganisationDbContext, Task<T>> read)
    {
        await using OrganisationDbContext context = OrganisationTestContext.Store(_database);
        return await read(context);
    }

    private Task<int> CountMembershipsAsync() =>
        ReadAsync(c => c.Memberships.IgnoreQueryFilters().CountAsync(Stubs.Ct));

    [Fact]
    public async Task TheFirstRunCreatesTheDemoOrganisation()
    {
        await SeedAsync();

        OrganisationAggregate organisation = await ReadAsync(c => c.Organisations.SingleAsync(Stubs.Ct));

        Assert.Equal(new TenantId(OrganisationDevSeed.TenantId), organisation.Id);
        Assert.Equal("DEMO", organisation.Code.Value);
        Assert.Equal("Demo Organisation", organisation.Name.English);
        Assert.Equal("المنشأة التجريبية", organisation.Name.Arabic);
        Assert.True(organisation.IsActive);
    }

    [Fact]
    public async Task TheFirstRunCreatesTwoBranchesInTheDemoTenant()
    {
        await SeedAsync();

        List<Branch> branches = await ReadAsync(c => c.Branches.OrderBy(b => b.Id.Value).ToListAsync(Stubs.Ct));

        Assert.Equal(["JED", "RYD"], branches.Select(b => b.Code.Value).Order(StringComparer.Ordinal));
        Assert.All(branches, b => Assert.Equal(new TenantId(OrganisationDevSeed.TenantId), b.TenantId));
        Assert.Equal(new BranchId(OrganisationDevSeed.RiyadhBranchId), branches.Single(b => b.Code.Value == "RYD").Id);
        Assert.Equal(new BranchId(OrganisationDevSeed.JeddahBranchId), branches.Single(b => b.Code.Value == "JED").Id);
    }

    [Fact]
    public async Task TheFirstRunCreatesThreeDepartmentsUnderTheRightBranches()
    {
        await SeedAsync();

        List<Department> departments = await ReadAsync(c => c.Departments.ToListAsync(Stubs.Ct));

        Assert.Equal(["JED-CARE", "RYD-BILL", "RYD-CARE"], departments.Select(d => d.Code.Value).Order(StringComparer.Ordinal));
        Assert.Equal(
            new BranchId(OrganisationDevSeed.RiyadhBranchId),
            departments.Single(d => d.Id == new DepartmentId(OrganisationDevSeed.RiyadhCareDepartmentId)).BranchId);
        Assert.Equal(
            new BranchId(OrganisationDevSeed.JeddahBranchId),
            departments.Single(d => d.Id == new DepartmentId(OrganisationDevSeed.JeddahCareDepartmentId)).BranchId);
    }

    [Fact]
    public async Task TheFirstRunCreatesElevenMembershipsWithTheWellKnownIdsAndRoles()
    {
        await SeedAsync();

        List<Membership> memberships = await ReadAsync(c => c.Memberships.ToListAsync(Stubs.Ct));

        Assert.Equal(11, memberships.Count);
        Membership supervisorCare = memberships.Single(m => m.Id == new MembershipId(OrganisationDevSeed.SupervisorCareMembershipId));
        Assert.Equal(SystemRole.Supervisor, supervisorCare.Role);
        Assert.Equal(OrganisationDevSeed.SupervisorUserId, supervisorCare.UserId);
        Assert.Equal(new DepartmentId(OrganisationDevSeed.RiyadhCareDepartmentId), supervisorCare.DepartmentId);
        Membership supervisorBilling = memberships.Single(m => m.Id == new MembershipId(OrganisationDevSeed.SupervisorBillingMembershipId));
        Assert.Equal(SystemRole.Agent, supervisorBilling.Role);
        Assert.Equal(new DepartmentId(OrganisationDevSeed.RiyadhBillingDepartmentId), supervisorBilling.DepartmentId);
        Assert.Equal(3, memberships.Count(m => m.UserId == OrganisationDevSeed.AdminUserId));
        Assert.All(
            memberships.Where(m => m.UserId == OrganisationDevSeed.AdminUserId),
            m => Assert.Equal(SystemRole.Administrator, m.Role));
        Assert.Equal(2, memberships.Count(m => m.UserId == OrganisationDevSeed.SupervisorBUserId));
        Assert.All(memberships, m => Assert.Equal(new TenantId(OrganisationDevSeed.TenantId), m.TenantId));
    }

    [Fact]
    public async Task TheFirstRunCreatesTheTwoSvgLogosWithTheWellKnownIds()
    {
        await SeedAsync();

        List<BrandLogo> logos = await ReadAsync(c => c.BrandLogos.ToListAsync(Stubs.Ct));

        Assert.Equal(2, logos.Count);
        Assert.All(logos, l => Assert.Equal(LogoContentType.Svg, l.ContentType));
        Assert.Contains(logos, l => l.Id == new BrandLogoId(OrganisationDevSeed.DemoLightLogoId));
        Assert.Contains(logos, l => l.Id == new BrandLogoId(OrganisationDevSeed.DemoDarkLogoId));
        Assert.All(logos, l => Assert.True(l.BelongsTo(new TenantId(OrganisationDevSeed.TenantId))));
    }

    [Fact]
    public async Task TheFirstRunAppliesTheDemoBranding()
    {
        await SeedAsync();

        OrganisationAggregate organisation = await ReadAsync(c => c.Organisations.SingleAsync(Stubs.Ct));

        Assert.NotNull(organisation.Branding);
        Assert.Equal(ArabicFont.Tajawal, organisation.Branding.ArabicFont);
        Assert.Equal("#0E6655", organisation.Branding.Palette.Primary.Hex);
        Assert.Equal("#B7791F", organisation.Branding.Palette.Accent.Hex);
        Assert.Equal(new BrandLogoId(OrganisationDevSeed.DemoLightLogoId), organisation.Branding.LightLogo);
        Assert.Equal(new BrandLogoId(OrganisationDevSeed.DemoDarkLogoId), organisation.Branding.DarkLogo);
        Assert.True(organisation.CanRevertBranding);
        Assert.Null(organisation.PreviousBranding);
    }

    [Fact]
    public async Task ASecondRunAddsNothing()
    {
        await SeedAsync();
        (int Organisations, int Branches, int Departments, int Memberships, int Logos) first = await CountsAsync();
        Guid[] ids = await ReadAsync(c => c.Memberships.Select(m => m.Id.Value).OrderBy(i => i).ToArrayAsync(Stubs.Ct));

        await SeedAsync();

        Assert.Equal(first, await CountsAsync());
        Assert.Equal(ids, await ReadAsync(c => c.Memberships.Select(m => m.Id.Value).OrderBy(i => i).ToArrayAsync(Stubs.Ct)));
    }

    [Fact]
    public async Task ASoftDeletedSeedMembershipIsNeitherResurrectedNorDuplicated()
    {
        await SeedAsync();
        var removedId = new MembershipId(OrganisationDevSeed.AgentACareMembershipId);
        await using (OrganisationDbContext store = OrganisationTestContext.Store(_database))
        {
            store.Memberships.Remove(await store.Memberships.SingleAsync(m => m.Id == removedId, Stubs.Ct));
            await store.SaveChangesAsync(Stubs.Ct);
        }

        await SeedAsync();

        Assert.Equal(11, await CountMembershipsAsync());
        Membership removed = await ReadAsync(c => c.Memberships.IgnoreQueryFilters().SingleAsync(m => m.Id == removedId, Stubs.Ct));
        Assert.True(removed.IsDeleted);
        Assert.Equal(10, await ReadAsync(c => c.Memberships.CountAsync(Stubs.Ct)));
    }

    [Fact]
    public async Task AnAdministratorsRevertToTheDefaultsIsNotOverwritten()
    {
        await SeedAsync();
        await using (OrganisationDbContext store = OrganisationTestContext.Store(_database))
        {
            OrganisationAggregate organisation = await store.Organisations.SingleAsync(Stubs.Ct);
            organisation.RevertBranding();
            await store.SaveChangesAsync(Stubs.Ct);
        }

        await SeedAsync();

        OrganisationAggregate after = await ReadAsync(c => c.Organisations.SingleAsync(Stubs.Ct));
        Assert.Null(after.Branding);
        Assert.True(after.CanRevertBranding);
    }

    [Fact]
    public async Task CustomBrandingIsNotOverwritten()
    {
        await SeedAsync();
        await using (OrganisationDbContext store = OrganisationTestContext.Store(_database))
        {
            OrganisationAggregate organisation = await store.Organisations.SingleAsync(Stubs.Ct);
            organisation.ChangeBranding(Brandings.Valid(english: "Mine"));
            await store.SaveChangesAsync(Stubs.Ct);
        }

        await SeedAsync();

        OrganisationAggregate after = await ReadAsync(c => c.Organisations.SingleAsync(Stubs.Ct));
        Assert.Equal("Mine", after.Branding?.DisplayName.English);
    }

    [Fact]
    public async Task TheSeederRunsUnderAnUnrestrictedScope()
    {
        // A scoped caller could not write the demo tenant's rows; the seeder runs with no HttpContext, so its scope is unrestricted.
        await using OrganisationDbContext context = OrganisationTestContext.Create(_database, CurrentUsers.Unrestricted());

        await new OrganisationSeeder(context).SeedAsync(Stubs.Ct);

        Assert.True(await context.Branches.AnyAsync(Stubs.Ct));
    }

    private Task<(int Organisations, int Branches, int Departments, int Memberships, int Logos)> CountsAsync() =>
        ReadAsync(async c => (
            await c.Organisations.IgnoreQueryFilters().CountAsync(Stubs.Ct),
            await c.Branches.IgnoreQueryFilters().CountAsync(Stubs.Ct),
            await c.Departments.IgnoreQueryFilters().CountAsync(Stubs.Ct),
            await c.Memberships.IgnoreQueryFilters().CountAsync(Stubs.Ct),
            await c.BrandLogos.IgnoreQueryFilters().CountAsync(Stubs.Ct)));
}
