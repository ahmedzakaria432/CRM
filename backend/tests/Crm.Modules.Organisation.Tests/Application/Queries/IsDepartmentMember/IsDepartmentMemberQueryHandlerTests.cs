using Crm.BuildingBlocks.Application.Authorization;
using Crm.Modules.Organisation.Application.Queries.IsDepartmentMember;
using Crm.Modules.Organisation.Domain;
using Crm.Modules.Organisation.Infrastructure.Persistence;
using Crm.Modules.Organisation.Tests.Support;
using Crm.SharedKernel.Results;
using Crm.SharedKernel.Scoping;

namespace Crm.Modules.Organisation.Tests.Application.Queries.IsDepartmentMember;

public sealed class IsDepartmentMemberQueryHandlerTests
{
    private readonly string _database = OrganisationTestContext.NewDatabaseName();

    private async Task<Result<bool>> RunAsync(Guid user, DepartmentId department, TenantId? tenant = null)
    {
        await using OrganisationDbContext context = OrganisationTestContext.Create(_database, CurrentUsers.Admin(tenant));
        var handler = new IsDepartmentMemberQueryHandler(OrganisationTestContext.Repo<Membership, MembershipId>(context));
        return await handler.Handle(new IsDepartmentMemberQuery(user, department.Value), Stubs.Ct);
    }

    private async Task<Membership> SeedMembershipAsync()
    {
        SeedData data = await SeedData.StandardAsync(_database);
        await using OrganisationDbContext store = OrganisationTestContext.Store(_database);
        Membership membership = Memberships.New(data.RiyadhCare, Ids.User);
        store.Memberships.Add(membership);
        await store.SaveChangesAsync(Stubs.Ct);
        return membership;
    }

    [Fact]
    public async Task AMemberIsTrue()
    {
        await SeedMembershipAsync();

        Assert.True((await RunAsync(Ids.User, Ids.DeptA1)).Value);
    }

    [Fact]
    public async Task ANonMemberIsFalse()
    {
        await SeedMembershipAsync();

        Assert.False((await RunAsync(Ids.OtherUser, Ids.DeptA1)).Value);
        Assert.False((await RunAsync(Ids.User, Ids.DeptA2)).Value);
    }

    [Fact]
    public async Task ARemovedMembershipIsFalse()
    {
        Membership membership = await SeedMembershipAsync();
        await using (OrganisationDbContext store = OrganisationTestContext.Store(_database))
        {
            store.Memberships.Remove(store.Memberships.Single(m => m.Id == membership.Id));
            await store.SaveChangesAsync(Stubs.Ct);
        }

        Assert.False((await RunAsync(Ids.User, Ids.DeptA1)).Value);
    }

    [Fact]
    public async Task AMembershipOfAnotherTenantIsNotVisible()
    {
        await SeedMembershipAsync();

        Assert.False((await RunAsync(Ids.User, Ids.DeptA1, Ids.TenantB)).Value);
    }

    [Fact]
    public void TheQueryRequiresAnAuthenticatedUser()
    {
        Assert.True(Stubs.HasAttribute<RequireAuthenticatedUserAttribute>(typeof(IsDepartmentMemberQuery)));
    }
}
