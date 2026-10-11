using Crm.BuildingBlocks.Application.Exceptions;
using Crm.Modules.Organisation.Domain;
using Crm.Modules.Organisation.Infrastructure.Persistence;
using Crm.Modules.Organisation.Tests.Support;
using Crm.SharedKernel.Scoping;
using Microsoft.EntityFrameworkCore;
using OrganisationAggregate = Crm.Modules.Organisation.Domain.Organisation;

namespace Crm.Modules.Organisation.Tests.Infrastructure;

public sealed class OrganisationDbContextTests
{
    private readonly string _database = OrganisationTestContext.NewDatabaseName();

    private async Task SeedAsync()
    {
        await SeedData.StandardAsync(_database);
        await using OrganisationDbContext store = OrganisationTestContext.Store(_database);
        store.Organisations.AddRange(Organisations.New("ORG-A", "A", Ids.TenantA), Organisations.New("ORG-B", "B", Ids.TenantB));
        store.BrandLogos.AddRange(Logos.Png(Ids.TenantA), Logos.Svg(Ids.TenantB));
        await store.SaveChangesAsync(Stubs.Ct);
    }

    [Fact]
    public void TheSchemaIsOrganisation()
    {
        Assert.Equal("organisation", OrganisationDbContext.SchemaName);
        using OrganisationDbContext context = OrganisationTestContext.Store(_database);

        Assert.Equal("organisation", context.Model.GetDefaultSchema());
    }

    [Fact]
    public void TheFiveDbSetsExistAndMapTheAggregates()
    {
        using OrganisationDbContext context = OrganisationTestContext.Store(_database);

        Assert.NotNull(context.Organisations);
        Assert.NotNull(context.Branches);
        Assert.NotNull(context.Departments);
        Assert.NotNull(context.Memberships);
        Assert.NotNull(context.BrandLogos);
        Assert.NotNull(context.Model.FindEntityType(typeof(OrganisationAggregate)));
        Assert.NotNull(context.Model.FindEntityType(typeof(BrandLogo)));
    }

    [Fact]
    public async Task ACallerCannotWriteABranchOfAnotherTenant()
    {
        await using OrganisationDbContext context = OrganisationTestContext.Create(_database, CurrentUsers.Admin(Ids.TenantA));
        context.Branches.Add(Branches.New(Ids.TenantB, "NEW"));

        var exception = await Assert.ThrowsAsync<OrganisationScopeViolationException>(() => context.SaveChangesAsync(Stubs.Ct));

        Assert.Equal("Write", exception.Operation);
        Assert.Equal(nameof(Branch), exception.EntityType);
    }

    [Fact]
    public async Task ACallerCanWriteABranchOfItsOwnTenant()
    {
        await using OrganisationDbContext context = OrganisationTestContext.Create(_database, CurrentUsers.Admin(Ids.TenantA));
        context.Branches.Add(Branches.New(Ids.TenantA, "NEW"));

        await context.SaveChangesAsync(Stubs.Ct);

        await using OrganisationDbContext read = OrganisationTestContext.Store(_database);
        Assert.Equal("NEW", Assert.Single(await read.Branches.ToListAsync(Stubs.Ct)).Code.Value);
    }

    [Fact]
    public async Task ReadsOfAnotherTenantsBranchesReturnNothing()
    {
        await SeedAsync();
        await using OrganisationDbContext context = OrganisationTestContext.Create(_database, CurrentUsers.Admin(Ids.TenantB));

        List<Branch> visible = await context.Branches.ToListAsync(Stubs.Ct);

        Assert.Equal("OTH", Assert.Single(visible).Code.Value);
    }

    [Fact]
    public async Task AnAnonymousCallerSeesNoBranchesDepartmentsOrMemberships()
    {
        await SeedAsync();
        await using OrganisationDbContext context = OrganisationTestContext.Create(_database, CurrentUsers.Anonymous());

        Assert.Empty(await context.Branches.ToListAsync(Stubs.Ct));
        Assert.Empty(await context.Departments.ToListAsync(Stubs.Ct));
        Assert.Empty(await context.Memberships.ToListAsync(Stubs.Ct));
    }

    [Fact]
    public async Task RemovingAMembershipSoftDeletesIt()
    {
        SeedData data = await SeedData.StandardAsync(_database);
        var membership = Memberships.New(data.RiyadhCare);
        await using (OrganisationDbContext seed = OrganisationTestContext.Store(_database))
        {
            seed.Memberships.Add(membership);
            await seed.SaveChangesAsync(Stubs.Ct);
        }

        await using (OrganisationDbContext context = OrganisationTestContext.Create(_database, CurrentUsers.Admin(Ids.TenantA)))
        {
            context.Memberships.Remove(await context.Memberships.SingleAsync(m => m.Id == membership.Id, Stubs.Ct));
            await context.SaveChangesAsync(Stubs.Ct);
        }

        await using OrganisationDbContext read = OrganisationTestContext.Store(_database);
        Membership row = await read.Memberships.IgnoreQueryFilters().SingleAsync(m => m.Id == membership.Id, Stubs.Ct);
        Assert.True(row.IsDeleted);
        Assert.NotNull(row.DeletedAtUtc);
        Assert.Empty(await read.Memberships.Where(m => m.Id == membership.Id).ToListAsync(Stubs.Ct));
    }

    [Fact]
    public async Task OrganisationsAreNotTenantScopedSoAnAnonymousCallerReadsThem()
    {
        await SeedAsync();
        await using OrganisationDbContext context = OrganisationTestContext.Create(_database, CurrentUsers.Anonymous());

        List<OrganisationAggregate> organisations = await context.Organisations.ToListAsync(Stubs.Ct);

        Assert.Equal(2, organisations.Count);
    }

    [Fact]
    public async Task BrandLogosAreNotTenantScopedSoAnAnonymousCallerReadsThem()
    {
        await SeedAsync();
        await using OrganisationDbContext context = OrganisationTestContext.Create(_database, CurrentUsers.Anonymous());

        Assert.Equal(2, await context.BrandLogos.CountAsync(Stubs.Ct));
    }

    [Fact]
    public async Task EntitiesAreAuditedWithTheCallerAndTheFixedClock()
    {
        await using OrganisationDbContext context = OrganisationTestContext.Create(_database, CurrentUsers.Admin(Ids.TenantA));
        Branch branch = Branches.New(Ids.TenantA, "AUD");
        context.Branches.Add(branch);

        await context.SaveChangesAsync(Stubs.Ct);

        Assert.Equal(CurrentUsers.DefaultUserId, branch.CreatedBy);
        Assert.Equal(Clock.Now, branch.CreatedAtUtc);
    }
}
