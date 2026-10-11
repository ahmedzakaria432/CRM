using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Identity.Application;
using Crm.Modules.Identity.Application.Queries.GetMe;
using Crm.Modules.Identity.Application.Queries.GetPortalMe;
using Crm.Modules.Identity.Domain;
using Crm.Modules.Identity.Tests.Support;
using Crm.SharedKernel.Results;
using Crm.SharedKernel.Scoping;

namespace Crm.Modules.Identity.Tests.Application.Queries;

public sealed class GetMeQueryHandlerTests
{
    private readonly IRepository<User, UserId> _users = Substitute.For<IRepository<User, UserId>>();

    private Task<Result<MeDto>> Run(ICurrentUserService caller) =>
        new GetMeQueryHandler(_users, caller).Handle(new GetMeQuery(), Stubs.Ct);

    private static ICurrentUserService CallerWith(params ScopeMembership[] memberships) => CurrentUsers.Agent(Scopes.AgentOfTenantA(memberships));

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-guid")]
    public async Task ANonGuidUserIsUnauthenticatedWithNoRepositoryCall(string? userId)
    {
        Result<MeDto> result = await Run(CurrentUsers.WithIds(Scope.OfTenantA(), userId, null));

        Expect.Failure(result, "Authorization.Unauthenticated", ErrorType.Unauthorized);
        await _users.DidNotReceive().GetByIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AMissingUserIsUnauthenticated() =>
        Expect.Failure(await Run(CallerWith()), "Authorization.Unauthenticated", ErrorType.Unauthorized);

    [Fact]
    public async Task AnInactiveUserIsUnauthenticatedSoALiveTokenDiesHere()
    {
        _users.Holds(Users.Inactive());

        Expect.Failure(await Run(CallerWith()), "Authorization.Unauthenticated", ErrorType.Unauthorized);
    }

    [Fact]
    public async Task SuccessMapsTheUserAndTheScope()
    {
        _users.Holds(Users.New(locale: "ar"));

        Result<MeDto> result = await Run(CallerWith(Scopes.Member(Ids.DeptA1, Ids.BranchA1, CrmRoles.Agent)));

        MeDto me = result.Value;
        Assert.Equal(Ids.User, me.Id);
        Assert.Equal("agent@example.com", me.Email);
        Assert.Equal("Omar Haddad", me.DisplayName);
        Assert.Equal("ar", me.PreferredLocale);
        Assert.Equal(Ids.TenantA.Value, me.TenantId);
        Assert.Null(me.ActiveDepartmentId);
        Assert.Equal(new MeMembershipDto(Ids.DeptA1.Value, Ids.BranchA1.Value, "Agent"), Assert.Single(me.Memberships));
        Assert.Equal(["Agent"], me.Roles);
    }

    [Fact]
    public async Task AnAgentHasNoAssignPermission()
    {
        _users.Holds(Users.New());

        Result<MeDto> result = await Run(CallerWith(Scopes.Member(Ids.DeptA1, Ids.BranchA1, CrmRoles.Agent)));

        Assert.DoesNotContain("tickets.assign", result.Value.Permissions);
        Assert.Contains("tickets.read", result.Value.Permissions);
    }

    [Fact]
    public async Task ASupervisorHasTheAssignPermission()
    {
        _users.Holds(Users.New());

        Result<MeDto> result = await Run(CallerWith(Scopes.Member(Ids.DeptA1, Ids.BranchA1, CrmRoles.Supervisor)));

        Assert.Contains("tickets.assign", result.Value.Permissions);
    }

    [Fact]
    public async Task AnAdministratorHasAdminUsersButNoPortalPermissions()
    {
        _users.Holds(Users.New());

        Result<MeDto> result = await Run(CallerWith(Scopes.Member(Ids.DeptA1, Ids.BranchA1, CrmRoles.Administrator)));

        Assert.Contains("admin.users", result.Value.Permissions);
        Assert.DoesNotContain(result.Value.Permissions, p => p.StartsWith("portal.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ANarrowedScopeShrinksTheEffectiveRolesAndPermissions()
    {
        _users.Holds(Users.New());
        ScopeMembership[] memberships =
        [
            Scopes.Member(Ids.DeptA1, Ids.BranchA1, CrmRoles.Supervisor),
            Scopes.Member(Ids.DeptA2, Ids.BranchA2, CrmRoles.Agent),
        ];

        Result<MeDto> result = await Run(CurrentUsers.Agent(Scopes.Narrowed(Ids.TenantA, memberships, Ids.DeptA2)));

        MeDto me = result.Value;
        Assert.Equal(Ids.DeptA2.Value, me.ActiveDepartmentId);
        Assert.Equal(["Agent"], me.Roles);
        Assert.DoesNotContain("tickets.assign", me.Permissions);
    }

    [Fact]
    public async Task MembershipsListsEveryMembershipEvenOutsideTheActiveDepartment()
    {
        _users.Holds(Users.New());
        ScopeMembership[] memberships =
        [
            Scopes.Member(Ids.DeptA1, Ids.BranchA1, CrmRoles.Supervisor),
            Scopes.Member(Ids.DeptA2, Ids.BranchA2, CrmRoles.Agent),
        ];

        Result<MeDto> result = await Run(CurrentUsers.Agent(Scopes.Narrowed(Ids.TenantA, memberships, Ids.DeptA2)));

        Assert.Equal(2, result.Value.Memberships.Count);
    }

    [Fact]
    public async Task AUserWithNoMembershipsHasNoPermissions()
    {
        _users.Holds(Users.New());

        Result<MeDto> result = await Run(CallerWith());

        Assert.Empty(result.Value.Permissions);
        Assert.Empty(result.Value.Roles);
        Assert.Empty(result.Value.Memberships);
    }
}

public sealed class GetPortalMeQueryHandlerTests
{
    private readonly IRepository<PortalAccount, PortalAccountId> _accounts = Substitute.For<IRepository<PortalAccount, PortalAccountId>>();

    private Task<Result<PortalMeDto>> Run(ICurrentUserService caller) =>
        new GetPortalMeQueryHandler(_accounts, caller).Handle(new GetPortalMeQuery(), Stubs.Ct);

    [Fact]
    public async Task ANonGuidUserIsPortalOnly()
    {
        Result<PortalMeDto> result = await Run(CurrentUsers.WithIds(Scopes.Portal(Ids.TenantA, Ids.Customer, Ids.Contact, true), "not-a-guid", null));

        Expect.Failure(result, "Authorization.PortalOnly", ErrorType.Unauthorized);
        await _accounts.DidNotReceive().GetByIdAsync(Arg.Any<PortalAccountId>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AMissingAccountIsPortalOnly() =>
        Expect.Failure(await Run(CurrentUsers.Portal(Ids.TenantA, Ids.Customer, Ids.Contact, true)), "Authorization.PortalOnly", ErrorType.Unauthorized);

    [Fact]
    public async Task AStaffPrincipalIsPortalOnly()
    {
        _accounts.Holds(Accounts.New());

        Result<PortalMeDto> result = await Run(CurrentUsers.WithIds(Scope.OfTenantA(), Ids.Account.ToString(), null));

        Expect.Failure(result, "Authorization.PortalOnly", ErrorType.Unauthorized);
    }

    [Fact]
    public async Task AContactMismatchIsPortalOnly()
    {
        _accounts.Holds(Accounts.New());

        Expect.Failure(await Run(CurrentUsers.Portal(Ids.TenantA, Ids.Customer, Ids.OtherContact, true)), "Authorization.PortalOnly", ErrorType.Unauthorized);
    }

    [Fact]
    public async Task ACustomerMismatchIsPortalOnly()
    {
        _accounts.Holds(Accounts.New());

        Expect.Failure(await Run(CurrentUsers.Portal(Ids.TenantA, Ids.OtherCustomer, Ids.Contact, true)), "Authorization.PortalOnly", ErrorType.Unauthorized);
    }

    [Fact]
    public async Task ATenantMismatchIsPortalOnly()
    {
        _accounts.Holds(Accounts.New());

        Expect.Failure(await Run(CurrentUsers.Portal(Ids.TenantB, Ids.Customer, Ids.Contact, true)), "Authorization.PortalOnly", ErrorType.Unauthorized);
    }

    [Fact]
    public async Task SuccessMapsTheAccountAndTheGrant()
    {
        _accounts.Holds(Accounts.New(locale: "ar"));

        Result<PortalMeDto> result = await Run(CurrentUsers.Portal(Ids.TenantA, Ids.Customer, Ids.Contact, true));

        PortalMeDto me = result.Value;
        Assert.Equal(Ids.Account, me.Id);
        Assert.Equal("customer@example.com", me.Email);
        Assert.Equal("Layla Saleh", me.DisplayName);
        Assert.Equal("ar", me.PreferredLocale);
        Assert.Equal(Ids.Customer, me.CustomerId);
        Assert.Equal(Ids.Contact, me.ContactId);
        Assert.True(me.IsAccountAdmin);
        Assert.Equal(["portal.account", "portal.tickets"], me.Permissions);
    }

    [Fact]
    public async Task TheAccountAdminFlagComesFromTheGrantNotTheAccount()
    {
        _accounts.Holds(Accounts.New());

        Result<PortalMeDto> result = await Run(CurrentUsers.Portal(Ids.TenantA, Ids.Customer, Ids.Contact, false));

        Assert.False(result.Value.IsAccountAdmin);
    }

    [Fact]
    public void ThePortalDtoHasNoMembershipsOrRolesMember()
    {
        string[] names = [.. typeof(PortalMeDto).GetProperties().Select(p => p.Name)];

        Assert.DoesNotContain("Memberships", names);
        Assert.DoesNotContain("Roles", names);
    }
}
