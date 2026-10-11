using Crm.BuildingBlocks.Application.Authorization;
using Crm.Modules.Organisation.Application;
using Crm.Modules.Organisation.Application.Queries.ListMemberships;
using Crm.Modules.Organisation.Domain;
using Crm.Modules.Organisation.Infrastructure.Persistence;
using Crm.Modules.Organisation.Tests.Support;
using Crm.SharedKernel.Results;
using Crm.SharedKernel.Scoping;
using Microsoft.EntityFrameworkCore;

namespace Crm.Modules.Organisation.Tests.Application.Queries.ListMemberships;

public sealed class ListMembershipsQueryHandlerTests
{
    private readonly string _database = OrganisationTestContext.NewDatabaseName();

    private async Task SeedAsync()
    {
        SeedData data = await SeedData.StandardAsync(_database);
        await using OrganisationDbContext store = OrganisationTestContext.Store(_database);
        Membership removed = Memberships.New(data.RiyadhCare, Ids.OtherUser);
        store.Memberships.AddRange(
            Memberships.New(data.RiyadhCare, Ids.User, SystemRole.Supervisor),
            Memberships.New(data.RiyadhBilling, Ids.User, SystemRole.Agent),
            Memberships.New(data.RiyadhBilling, Ids.OtherUser, SystemRole.Agent),
            Memberships.New(data.OtherDepartment, Ids.User, SystemRole.Agent),
            removed);
        await store.SaveChangesAsync(Stubs.Ct);

        store.Memberships.Remove(removed);
        await store.SaveChangesAsync(Stubs.Ct);
    }

    private async Task<IReadOnlyList<MembershipDto>> RunAsync(Guid? user, Guid? department, TenantId? tenant = null)
    {
        await using OrganisationDbContext context = OrganisationTestContext.Create(_database, CurrentUsers.Admin(tenant));
        var handler = new ListMembershipsQueryHandler(OrganisationTestContext.Repo<Membership, MembershipId>(context));
        Result<IReadOnlyList<MembershipDto>> result = await handler.Handle(new ListMembershipsQuery(user, department), Stubs.Ct);
        return result.Value;
    }

    [Fact]
    public async Task NoFilterReturnsTheTenantsLiveMemberships()
    {
        await SeedAsync();

        IReadOnlyList<MembershipDto> result = await RunAsync(null, null);

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task TheUserFilterKeepsOnlyThatUser()
    {
        await SeedAsync();

        IReadOnlyList<MembershipDto> result = await RunAsync(Ids.User, null);

        Assert.Equal(2, result.Count);
        Assert.All(result, m => Assert.Equal(Ids.User, m.UserId));
    }

    [Fact]
    public async Task TheDepartmentFilterKeepsOnlyThatDepartment()
    {
        await SeedAsync();

        IReadOnlyList<MembershipDto> result = await RunAsync(null, Ids.DeptA2.Value);

        Assert.Equal(2, result.Count);
        Assert.All(result, m => Assert.Equal(Ids.DeptA2.Value, m.DepartmentId));
    }

    [Fact]
    public async Task BothFiltersCombine()
    {
        await SeedAsync();

        MembershipDto membership = Assert.Single(await RunAsync(Ids.User, Ids.DeptA1.Value));

        Assert.Equal("Supervisor", membership.Role);
        Assert.Equal(Ids.BranchA1.Value, membership.BranchId);
    }

    [Fact]
    public async Task ASoftDeletedMembershipIsExcluded()
    {
        await SeedAsync();

        IReadOnlyList<MembershipDto> result = await RunAsync(Ids.OtherUser, Ids.DeptA1.Value);

        Assert.Empty(result);
    }

    [Fact]
    public async Task AnotherTenantsMembershipsAreExcluded()
    {
        await SeedAsync();

        IReadOnlyList<MembershipDto> result = await RunAsync(null, null, Ids.TenantB);

        MembershipDto membership = Assert.Single(result);
        Assert.Equal(Ids.DeptB1.Value, membership.DepartmentId);
    }

    [Fact]
    public async Task TheRemovedRowStillExistsInTheStore()
    {
        await SeedAsync();
        await using OrganisationDbContext store = OrganisationTestContext.Store(_database);

        int all = await store.Memberships.IgnoreQueryFilters().CountAsync(Stubs.Ct);

        Assert.Equal(5, all);
    }

    [Fact]
    public void TheQueryRequiresTheAdminOrganisationPermission()
    {
        Assert.Equal(CrmPermissions.AdminOrganisation, Stubs.PermissionOf(typeof(ListMembershipsQuery)));
    }
}
