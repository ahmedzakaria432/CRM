using Crm.BuildingBlocks.Application.Authorization;
using Crm.Modules.Organisation.Application.Queries.GetMembershipsOfUser;
using Crm.Modules.Organisation.Contracts;
using Crm.Modules.Organisation.Domain;
using Crm.Modules.Organisation.Infrastructure.Persistence;
using Crm.Modules.Organisation.Tests.Support;
using Crm.SharedKernel.Results;

namespace Crm.Modules.Organisation.Tests.Application.Queries.GetMembershipsOfUser;

/// <summary>The sign-in lookup runs with no scope at all (OrganisationScope.None): the specification lifts the scope filter.</summary>
public sealed class GetMembershipsOfUserQueryHandlerTests
{
    private readonly string _database = OrganisationTestContext.NewDatabaseName();

    private async Task<SeedData> SeedAsync()
    {
        SeedData data = await SeedData.StandardAsync(_database);
        await using OrganisationDbContext store = OrganisationTestContext.Store(_database);
        store.Memberships.AddRange(
            Memberships.New(data.RiyadhCare, Ids.User, SystemRole.Supervisor),
            Memberships.New(data.RiyadhBilling, Ids.User, SystemRole.Agent),
            Memberships.New(data.RiyadhCare, Ids.OtherUser, SystemRole.Agent));
        await store.SaveChangesAsync(Stubs.Ct);
        return data;
    }

    private async Task<Result<UserMemberships>> RunAsync(Guid user)
    {
        await using OrganisationDbContext context = OrganisationTestContext.Create(_database, CurrentUsers.Anonymous());
        var handler = new GetMembershipsOfUserQueryHandler(OrganisationTestContext.Repo<Membership, MembershipId>(context));
        return await handler.Handle(new GetMembershipsOfUserQuery(user), Stubs.Ct);
    }

    [Fact]
    public async Task NoMembershipsIsASuccessWithAnEmptyTenantAndList()
    {
        await SeedAsync();

        Result<UserMemberships> result = await RunAsync(Guid.NewGuid());

        Assert.True(result.IsSuccess);
        Assert.Equal(Guid.Empty, result.Value.TenantId);
        Assert.Empty(result.Value.Memberships);
    }

    [Fact]
    public async Task TheUserIdIsEchoedBack()
    {
        await SeedAsync();
        var user = Guid.NewGuid();

        Assert.Equal(user, (await RunAsync(user)).Value.UserId);
    }

    [Fact]
    public async Task SeveralMembershipsReturnTheTenantAndEveryRow()
    {
        SeedData data = await SeedAsync();

        UserMemberships result = (await RunAsync(Ids.User)).Value;

        Assert.Equal(Ids.TenantA.Value, result.TenantId);
        Assert.Equal(2, result.Memberships.Count);
        UserMembership care = result.Memberships.Single(m => m.DepartmentId == data.RiyadhCare.Id.Value);
        Assert.Equal("Supervisor", care.Role);
        Assert.Equal(Ids.BranchA1.Value, care.BranchId);
        Assert.NotEqual(Guid.Empty, care.MembershipId);
        Assert.Equal("Agent", result.Memberships.Single(m => m.DepartmentId == data.RiyadhBilling.Id.Value).Role);
    }

    [Fact]
    public async Task AnotherUsersRowsAreNotReturned()
    {
        await SeedAsync();

        UserMemberships result = (await RunAsync(Ids.OtherUser)).Value;

        Assert.Single(result.Memberships);
    }

    [Fact]
    public async Task ASoftDeletedMembershipIsNotReturnedEvenThoughTheScopeFilterIsLifted()
    {
        await SeedAsync();
        await using (OrganisationDbContext store = OrganisationTestContext.Store(_database))
        {
            Membership agent = store.Memberships.Single(m => m.UserId == Ids.User && m.Role == SystemRole.Agent);
            store.Memberships.Remove(agent);
            await store.SaveChangesAsync(Stubs.Ct);
        }

        UserMemberships result = (await RunAsync(Ids.User)).Value;

        Assert.Equal("Supervisor", Assert.Single(result.Memberships).Role);
    }

    [Fact]
    public async Task MembershipsOfAnotherTenantAreStillFoundBecauseTheCallerHasNoScope()
    {
        SeedData data = await SeedAsync();
        await using (OrganisationDbContext store = OrganisationTestContext.Store(_database))
        {
            store.Memberships.Add(Memberships.New(data.OtherDepartment, Ids.OtherUser));
            await store.SaveChangesAsync(Stubs.Ct);
        }

        UserMemberships result = (await RunAsync(Ids.OtherUser)).Value;

        Assert.Equal(2, result.Memberships.Count);
    }

    [Fact]
    public async Task TheTenantIsTheFirstMembershipsTenant()
    {
        // Pinned (Follow-up): the handler assumes every membership of a user shares one tenant and reports the first.
        SeedData data = await SeedAsync();
        await using (OrganisationDbContext store = OrganisationTestContext.Store(_database))
        {
            store.Memberships.Add(Memberships.New(data.OtherDepartment, Ids.User));
            await store.SaveChangesAsync(Stubs.Ct);
        }

        UserMemberships result = (await RunAsync(Ids.User)).Value;

        Assert.Equal(3, result.Memberships.Count);
        Assert.Contains(result.TenantId, new[] { Ids.TenantA.Value, Ids.TenantB.Value });
    }

    [Fact]
    public void TheQueryIsAnonymous()
    {
        Assert.True(Stubs.HasAttribute<AllowAnonymousRequestAttribute>(typeof(GetMembershipsOfUserQuery)));
    }
}
