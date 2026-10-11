using Crm.BuildingBlocks.Application.Exceptions;
using Crm.Modules.Organisation.Domain;
using Crm.Modules.Organisation.Infrastructure.Persistence;
using Crm.Modules.Organisation.Tests.Support;
using Crm.SharedKernel.Scoping;
using Microsoft.EntityFrameworkCore;
using OrganisationAggregate = Crm.Modules.Organisation.Domain.Organisation;

namespace Crm.Modules.Organisation.Tests.Infrastructure;

/// <summary>Only the module-specific mapping of the generic repository behaviour (the generic rules are tested with the
/// building blocks).</summary>
public sealed class RepositoryBehaviourWithOrganisationAggregatesTests
{
    private readonly string _database = OrganisationTestContext.NewDatabaseName();

    [Fact]
    public async Task ABranchOfAnotherTenantIsAScopeViolationNotANotFound()
    {
        SeedData data = await SeedData.StandardAsync(_database);
        await using OrganisationDbContext context = OrganisationTestContext.Create(_database, CurrentUsers.Admin(Ids.TenantA));

        var exception = await Assert.ThrowsAsync<OrganisationScopeViolationException>(
            () => OrganisationTestContext.Repo<Branch, BranchId>(context).GetByIdAsync(data.OtherBranch.Id, Stubs.Ct));

        Assert.Equal("Read", exception.Operation);
        Assert.Equal(nameof(Branch), exception.EntityType);
    }

    [Fact]
    public async Task AnUnknownBranchIdIsNull()
    {
        await SeedData.StandardAsync(_database);
        await using OrganisationDbContext context = OrganisationTestContext.Create(_database, CurrentUsers.Admin(Ids.TenantA));

        Branch? branch = await OrganisationTestContext.Repo<Branch, BranchId>(context).GetByIdAsync(BranchId.New(), Stubs.Ct);

        Assert.Null(branch);
    }

    [Fact]
    public async Task ABranchOfTheCallersTenantIsFound()
    {
        SeedData data = await SeedData.StandardAsync(_database);
        await using OrganisationDbContext context = OrganisationTestContext.Create(_database, CurrentUsers.Admin(Ids.TenantA));

        Branch? branch = await OrganisationTestContext.Repo<Branch, BranchId>(context).GetByIdAsync(data.Riyadh.Id, Stubs.Ct);

        Assert.Equal("RYD", branch?.Code.Value);
    }

    [Fact]
    public async Task ASoftDeletedMembershipIsNullNotAScopeViolation()
    {
        SeedData data = await SeedData.StandardAsync(_database);
        var membership = Memberships.New(data.RiyadhCare);
        await using (OrganisationDbContext seed = OrganisationTestContext.Store(_database))
        {
            seed.Memberships.Add(membership);
            await seed.SaveChangesAsync(Stubs.Ct);
            seed.Memberships.Remove(membership);
            await seed.SaveChangesAsync(Stubs.Ct);
        }

        await using OrganisationDbContext context = OrganisationTestContext.Create(_database, CurrentUsers.Admin(Ids.TenantA));
        Membership? found = await OrganisationTestContext.Repo<Membership, MembershipId>(context).GetByIdAsync(membership.Id, Stubs.Ct);

        Assert.Null(found);
    }

    [Fact]
    public async Task ADepartmentOfAnotherTenantIsAScopeViolation()
    {
        SeedData data = await SeedData.StandardAsync(_database);
        await using OrganisationDbContext context = OrganisationTestContext.Create(_database, CurrentUsers.Admin(Ids.TenantA));

        await Assert.ThrowsAsync<OrganisationScopeViolationException>(
            () => OrganisationTestContext.Repo<Department, DepartmentId>(context).GetByIdAsync(data.OtherDepartment.Id, Stubs.Ct));
    }

    [Fact]
    public async Task SetOriginalRowVersionSetsTheOriginalValueOnTheTrackedEntry()
    {
        SeedData data = await SeedData.StandardAsync(_database);
        await using OrganisationDbContext context = OrganisationTestContext.Create(_database, CurrentUsers.Admin(Ids.TenantA));
        var repository = OrganisationTestContext.Repo<Branch, BranchId>(context);
        Branch branch = await repository.GetByIdAsync(data.Riyadh.Id, Stubs.Ct) ?? throw new InvalidOperationException("seeded branch missing");
        byte[] clientVersion = Ids.RowVersion();

        repository.SetOriginalRowVersion(branch, clientVersion);

        Assert.Equal(clientVersion, context.Entry(branch).Property(b => b.RowVersion).OriginalValue);
    }

    [Fact]
    public async Task TheOrganisationIsFoundByTheCallersTenantId()
    {
        await using (OrganisationDbContext seed = OrganisationTestContext.Store(_database))
        {
            seed.Organisations.Add(Organisations.New("ORG-A", "A", Ids.TenantA));
            await seed.SaveChangesAsync(Stubs.Ct);
        }

        await using OrganisationDbContext context = OrganisationTestContext.Create(_database, CurrentUsers.Admin(Ids.TenantA));
        var organisation = await OrganisationTestContext.Repo<OrganisationAggregate, TenantId>(context).GetByIdAsync(Ids.TenantA, Stubs.Ct);

        Assert.Equal("ORG-A", organisation?.Code.Value);
    }
}
