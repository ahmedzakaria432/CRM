using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Authorization;
using Crm.Modules.Organisation.Application;
using Crm.Modules.Organisation.Application.Queries.GetMyScope;
using Crm.Modules.Organisation.Domain;
using Crm.Modules.Organisation.Infrastructure.Persistence;
using Crm.Modules.Organisation.Tests.Support;
using Crm.SharedKernel.Results;
using Crm.SharedKernel.Scoping;

namespace Crm.Modules.Organisation.Tests.Application.Queries.GetMyScope;

/// <summary>Runs against EF InMemory through the real repositories: the tenant filter applies for real.</summary>
public sealed class GetMyScopeQueryHandlerTests
{
    private readonly string _database = OrganisationTestContext.NewDatabaseName();

    private async Task<Result<MyScopeDto>> RunAsync(ICurrentUserService user)
    {
        await using OrganisationDbContext context = OrganisationTestContext.Create(_database, user);
        var handler = new GetMyScopeQueryHandler(
            user,
            OrganisationTestContext.Repo<Department, DepartmentId>(context),
            OrganisationTestContext.Repo<Branch, BranchId>(context));
        return await handler.Handle(new GetMyScopeQuery(), Stubs.Ct);
    }

    private static ICurrentUserService UserWith(params ScopeMembership[] memberships) =>
        CurrentUsers.Agent(Scopes.AgentOfTenantA(memberships));

    [Fact]
    public async Task NoMembershipsIsForbidden()
    {
        await SeedData.StandardAsync(_database);

        Result<MyScopeDto> result = await RunAsync(UserWith());

        Expect.Failure(result, "Organisation.NoMemberships", ErrorType.Forbidden);
    }

    [Fact]
    public async Task ASingleMembershipYieldsOneDepartmentWithLocalisedNames()
    {
        await SeedData.StandardAsync(_database);

        Result<MyScopeDto> result = await RunAsync(UserWith(Scopes.Member(Ids.DeptA1, Ids.BranchA1, CrmRoles.Supervisor)));

        MyDepartmentDto department = Assert.Single(result.Value.Departments);
        Assert.Equal(Ids.DeptA1.Value, department.DepartmentId);
        Assert.Equal("RYD-CARE", department.DepartmentCode);
        Assert.Equal("Care Riyadh", department.DepartmentName.En);
        Assert.Equal("اسم", department.DepartmentName.Ar);
        Assert.Equal(Ids.BranchA1.Value, department.BranchId);
        Assert.Equal("Riyadh", department.BranchName.En);
        Assert.Equal(CrmRoles.Supervisor, department.Role);
        Assert.True(department.IsActive);
    }

    [Fact]
    public async Task TheUserAndTenantComeFromTheCurrentUser()
    {
        await SeedData.StandardAsync(_database);

        Result<MyScopeDto> result = await RunAsync(UserWith(Scopes.Member(Ids.DeptA1, Ids.BranchA1)));

        Assert.Equal(Ids.User, result.Value.UserId);
        Assert.Equal(Ids.TenantA.Value, result.Value.TenantId);
        Assert.Null(result.Value.ActiveDepartmentId);
    }

    [Fact]
    public async Task TwoDepartmentsInTwoBranchesAreBothReturnedWithTheirOwnRoles()
    {
        await SeedData.StandardAsync(_database);

        Result<MyScopeDto> result = await RunAsync(UserWith(
            Scopes.Member(Ids.DeptA1, Ids.BranchA1, CrmRoles.Supervisor),
            Scopes.Member(Ids.DeptA3, Ids.BranchA2, CrmRoles.Agent)));

        Assert.Equal(2, result.Value.Departments.Count);
        MyDepartmentDto riyadh = result.Value.Departments.Single(d => d.DepartmentId == Ids.DeptA1.Value);
        MyDepartmentDto jeddah = result.Value.Departments.Single(d => d.DepartmentId == Ids.DeptA3.Value);
        Assert.Equal(CrmRoles.Supervisor, riyadh.Role);
        Assert.Equal(CrmRoles.Agent, jeddah.Role);
        Assert.Equal("Jeddah", jeddah.BranchName.En);
        Assert.Equal([CrmRoles.Agent, CrmRoles.Supervisor], result.Value.Roles);
    }

    [Fact]
    public async Task AnInactiveBranchMakesItsDepartmentInactive()
    {
        await SeedData.StandardAsync(_database, jeddahInactive: true);

        Result<MyScopeDto> result = await RunAsync(UserWith(
            Scopes.Member(Ids.DeptA1, Ids.BranchA1),
            Scopes.Member(Ids.DeptA3, Ids.BranchA2)));

        Assert.True(result.Value.Departments.Single(d => d.DepartmentId == Ids.DeptA1.Value).IsActive);
        Assert.False(result.Value.Departments.Single(d => d.DepartmentId == Ids.DeptA3.Value).IsActive);
    }

    [Fact]
    public async Task AnInactiveDepartmentIsReportedInactive()
    {
        await using (OrganisationDbContext store = OrganisationTestContext.Store(_database))
        {
            Branch branch = Branches.New(Ids.TenantA, "RYD", id: Ids.BranchA1);
            Department department = Departments.New(branch, "OLD", id: Ids.DeptA1);
            department.Deactivate();
            store.Branches.Add(branch);
            store.Departments.Add(department);
            await store.SaveChangesAsync(Stubs.Ct);
        }

        Result<MyScopeDto> result = await RunAsync(UserWith(Scopes.Member(Ids.DeptA1, Ids.BranchA1)));

        Assert.False(Assert.Single(result.Value.Departments).IsActive);
    }

    [Fact]
    public async Task AMembershipWhoseDepartmentRowIsMissingIsSkipped()
    {
        await SeedData.StandardAsync(_database);
        var ghost = new DepartmentId(Guid.NewGuid());

        Result<MyScopeDto> result = await RunAsync(UserWith(
            Scopes.Member(Ids.DeptA1, Ids.BranchA1),
            Scopes.Member(ghost, Ids.BranchA1)));

        Assert.Equal(Ids.DeptA1.Value, Assert.Single(result.Value.Departments).DepartmentId);
    }

    [Fact]
    public async Task AMembershipWhoseBranchRowIsMissingIsSkipped()
    {
        await SeedData.StandardAsync(_database);
        var ghostBranch = new BranchId(Guid.NewGuid());

        Result<MyScopeDto> result = await RunAsync(UserWith(
            Scopes.Member(Ids.DeptA1, Ids.BranchA1),
            Scopes.Member(Ids.DeptA2, ghostBranch)));

        Assert.Equal(Ids.DeptA1.Value, Assert.Single(result.Value.Departments).DepartmentId);
    }

    [Fact]
    public async Task TwoRolesInOneDepartmentGiveTwoRowsFromOneLookup()
    {
        await SeedData.StandardAsync(_database);

        Result<MyScopeDto> result = await RunAsync(UserWith(
            Scopes.Member(Ids.DeptA1, Ids.BranchA1, CrmRoles.Agent),
            Scopes.Member(Ids.DeptA1, Ids.BranchA1, CrmRoles.Supervisor)));

        Assert.Equal(2, result.Value.Departments.Count);
        Assert.Equal([CrmRoles.Agent, CrmRoles.Supervisor], result.Value.Departments.Select(d => d.Role));
    }

    [Fact]
    public async Task ANarrowedScopeReportsTheActiveDepartment()
    {
        await SeedData.StandardAsync(_database);
        ScopeMembership[] memberships = [Scopes.Member(Ids.DeptA1, Ids.BranchA1), Scopes.Member(Ids.DeptA3, Ids.BranchA2)];
        ICurrentUserService user = CurrentUsers.Agent(Scopes.Narrowed(Ids.TenantA, memberships, Ids.DeptA3));

        Result<MyScopeDto> result = await RunAsync(user);

        Assert.Equal(Ids.DeptA3.Value, result.Value.ActiveDepartmentId);
        Assert.Equal(2, result.Value.Departments.Count);
    }

    [Fact]
    public async Task ADepartmentOfAnotherTenantIsNotReturnedEvenIfTheScopeNamesIt()
    {
        await SeedData.StandardAsync(_database);

        Result<MyScopeDto> result = await RunAsync(UserWith(
            Scopes.Member(Ids.DeptA1, Ids.BranchA1),
            Scopes.Member(Ids.DeptB1, Ids.BranchB1)));

        Assert.Equal(Ids.DeptA1.Value, Assert.Single(result.Value.Departments).DepartmentId);
    }

    [Fact]
    public async Task ANonGuidUserIdThrowsAFormatException()
    {
        // Pinned (Follow-up): the handler parses the staff user id with Guid.Parse, so a non-Guid subject is a 500.
        await SeedData.StandardAsync(_database);
        ICurrentUserService user = CurrentUsers.WithUserId(Scopes.AgentOfTenantA(Scopes.Member(Ids.DeptA1, Ids.BranchA1)), "user-1");

        await Assert.ThrowsAsync<FormatException>(() => RunAsync(user));
    }

    [Fact]
    public void TheQueryRequiresAnAuthenticatedUser()
    {
        Assert.True(Stubs.HasAttribute<RequireAuthenticatedUserAttribute>(typeof(GetMyScopeQuery)));
    }
}
