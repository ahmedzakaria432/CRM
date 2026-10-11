using System.Security.Claims;
using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Infrastructure.Identity;
using Crm.BuildingBlocks.Tests.Support;
using Crm.BuildingBlocks.Tests.Support.Http;
using Microsoft.AspNetCore.Http;

namespace Crm.BuildingBlocks.Tests.Infrastructure.Identity;

public sealed class CurrentUserServiceTests
{
    private static readonly string TenantClaim = ScopeFixtures.TenantAGuid.ToString();

    private static string Membership(Guid department, Guid branch, string role) => $"{department}|{branch}|{role}";

    private static List<Claim> StaffClaims(params string[] memberships)
    {
        List<Claim> claims =
        [
            new(CrmClaimTypes.UserId, "user-7"),
            new(CrmClaimTypes.SessionId, "session-3"),
            new(CrmClaimTypes.TenantId, TenantClaim),
        ];
        claims.AddRange(memberships.Select(m => new Claim(CrmClaimTypes.Membership, m)));
        return claims;
    }

    private static List<Claim> PortalClaims(Action<List<Claim>>? mutate = null)
    {
        List<Claim> claims =
        [
            new(CrmClaimTypes.UserId, "portal-user"),
            new(CrmClaimTypes.TenantId, TenantClaim),
            new(CrmClaimTypes.PortalCustomerId, Scopes.PortalCustomerGuid.ToString()),
            new(CrmClaimTypes.PortalContactId, Scopes.PortalContactGuid.ToString()),
            new(CrmClaimTypes.PortalAccountAdmin, "true"),
            new(CrmClaimTypes.PortalBranchId, ScopeFixtures.BranchA1Guid.ToString()),
            new(CrmClaimTypes.PortalDepartmentId, ScopeFixtures.DeptA1Guid.ToString()),
        ];
        mutate?.Invoke(claims);
        return claims;
    }

    private static CurrentUserService ServiceFor(HttpContext? context)
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(context);
        return new CurrentUserService(accessor);
    }

    private static CurrentUserService Staff(string? scopeHeader, params string[] memberships)
    {
        var context = HttpContexts.Authenticated(StaffClaims(memberships), CrmAuthenticationSchemes.Staff);
        if (scopeHeader is not null)
        {
            context.WithHeader(OrganisationScope.HeaderName, scopeHeader);
        }

        return ServiceFor(context);
    }

    private static readonly string A1 = Membership(ScopeFixtures.DeptA1Guid, ScopeFixtures.BranchA1Guid, CrmRoles.Agent);
    private static readonly string A2 = Membership(ScopeFixtures.DeptA2Guid, ScopeFixtures.BranchA2Guid, CrmRoles.Administrator);

    // --- no context / unauthenticated ---------------------------------------------------------------------

    [Fact]
    public void WithoutAnHttpContextTheScopeIsUnrestrictedAndTheUserIsTheSystem()
    {
        var service = ServiceFor(null);

        Assert.Same(OrganisationScope.Unrestricted, service.Scope);
        Assert.Null(service.UserId);
        Assert.Null(service.SessionId);
        Assert.Equal("system", service.UserIdOrSystem);
        Assert.Null(service.TenantId);
        Assert.Null(service.DepartmentId);
        Assert.Null(service.BranchId);
    }

    [Fact]
    public void AnUnauthenticatedRequestHasTheNoneScopeAndNoUserId()
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(CrmClaimTypes.UserId, "ghost")])),
        };
        var service = ServiceFor(context);

        Assert.Same(OrganisationScope.None, service.Scope);
        Assert.Null(service.UserId);
        Assert.Equal("system", service.UserIdOrSystem);
    }

    // --- staff --------------------------------------------------------------------------------------------

    [Fact]
    public void AStaffUserGetsItsTenantAndMemberships()
    {
        var service = Staff(null, A1, A2);

        Assert.Equal(ScopeFixtures.TenantA, service.Scope.Tenant);
        Assert.Equal(2, service.Scope.Memberships.Count);
        Assert.Equal(new ScopeMembership(ScopeFixtures.DeptA1, ScopeFixtures.BranchA1, CrmRoles.Agent), service.Scope.Memberships[0]);
        Assert.False(service.Scope.IsPortalCustomer);
        Assert.False(service.Scope.IsUnrestricted);
    }

    [Fact]
    public void TheStringMembersCarryTheTenantAndTheCommaJoinedEffectiveIds()
    {
        var service = Staff(null, A2, A1);

        Assert.Equal(TenantClaim, service.TenantId);
        Assert.Equal($"{ScopeFixtures.DeptA1Guid},{ScopeFixtures.DeptA2Guid}", service.DepartmentId);
        Assert.Equal($"{ScopeFixtures.BranchA1Guid},{ScopeFixtures.BranchA2Guid}", service.BranchId);
    }

    [Fact]
    public void UserIdAndSessionIdComeFromTheSubAndSidClaims()
    {
        var service = Staff(null, A1);

        Assert.Equal("user-7", service.UserId);
        Assert.Equal("session-3", service.SessionId);
        Assert.Equal("user-7", service.UserIdOrSystem);
    }

    [Fact]
    public void AStaffUserWithNoMembershipsHasNoDepartmentsOrBranches()
    {
        var service = Staff(null);

        Assert.Empty(service.Scope.Departments);
        Assert.Null(service.DepartmentId);
        Assert.Null(service.BranchId);
        Assert.Equal(TenantClaim, service.TenantId);
    }

    [Theory]
    [InlineData("not-a-membership")]
    [InlineData("a|b")]
    [InlineData("a|b|c|d")]
    [InlineData("zzzz|d1d1d1d1-0000-0000-0000-0000000000a1|Agent")]
    [InlineData("d1d1d1d1-0000-0000-0000-0000000000a1|zzzz|Agent")]
    [InlineData("")]
    public void AMalformedMembershipClaimIsSkippedNeverThrown(string malformed)
    {
        var service = Staff(null, malformed, A1);

        var membership = Assert.Single(service.Scope.Memberships);
        Assert.Equal(ScopeFixtures.DeptA1, membership.DepartmentId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("garbage")]
    [InlineData("")]
    public void AMissingOrGarbageTenantClaimGivesTheNoneScope(string? tenant)
    {
        List<Claim> claims = [new(CrmClaimTypes.UserId, "user-7"), new(CrmClaimTypes.Membership, A1)];
        if (tenant is not null)
        {
            claims.Add(new Claim(CrmClaimTypes.TenantId, tenant));
        }

        var service = ServiceFor(HttpContexts.Authenticated(claims, CrmAuthenticationSchemes.Staff));

        Assert.Same(OrganisationScope.None, service.Scope);
    }

    // --- X-Crm-Scope --------------------------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("all")]
    [InlineData("ALL")]
    [InlineData("All")]
    public void NoHeaderOrAllMeansNoNarrowing(string? header)
    {
        var service = Staff(header, A1, A2);

        Assert.Null(service.Scope.ActiveDepartment);
        Assert.False(service.Scope.ActiveScopeRejected);
        Assert.Equal(2, service.Scope.Departments.Count);
    }

    [Fact]
    public void ADepartmentTheUserBelongsToNarrowsTheScope()
    {
        var service = Staff(ScopeFixtures.DeptA2Guid.ToString(), A1, A2);

        Assert.Equal(ScopeFixtures.DeptA2, service.Scope.ActiveDepartment);
        Assert.Equal([ScopeFixtures.DeptA2], service.Scope.Departments);
        Assert.Equal(ScopeFixtures.DeptA2Guid.ToString(), service.DepartmentId);
        Assert.False(service.Scope.ActiveScopeRejected);
    }

    [Fact]
    public void ADepartmentTheUserDoesNotBelongToIsRejectedNeverWidened()
    {
        var service = Staff(ScopeFixtures.DeptB1Guid.ToString(), A1, A2);

        Assert.True(service.Scope.ActiveScopeRejected);
        Assert.Null(service.Scope.ActiveDepartment);
        Assert.Empty(service.Scope.Departments);
        Assert.Null(service.DepartmentId);
    }

    [Fact]
    public void ANonGuidHeaderIsRejected()
    {
        var service = Staff("not-a-guid", A1);

        Assert.True(service.Scope.ActiveScopeRejected);
        Assert.Empty(service.Scope.Departments);
    }

    [Fact]
    public void ARejectedScopeStillCarriesTheUsersTenantAndMemberships()
    {
        var service = Staff(ScopeFixtures.DeptB1Guid.ToString(), A1);

        Assert.Equal(ScopeFixtures.TenantA, service.Scope.Tenant);
        Assert.Single(service.Scope.Memberships);
    }

    // --- portal -------------------------------------------------------------------------------------------

    [Fact]
    public void APortalPrincipalResolvesToACustomerGrant()
    {
        var service = ServiceFor(HttpContexts.Authenticated(PortalClaims(), CrmAuthenticationSchemes.Portal));

        Assert.True(service.Scope.IsPortalCustomer);
        var grant = Assert.IsType<PortalCustomerGrant>(service.Scope.PortalCustomer);
        Assert.Equal(Scopes.PortalCustomerGuid, grant.CustomerId);
        Assert.Equal(Scopes.PortalContactGuid, grant.ContactId);
        Assert.True(grant.IsAccountAdmin);
        Assert.Equal(ScopeFixtures.BranchA1, grant.HomeBranchId);
        Assert.Equal(ScopeFixtures.DeptA1, grant.HomeDepartmentId);
        Assert.Equal(ScopeFixtures.TenantA, service.Scope.Tenant);
        Assert.Equal(TenantClaim, service.TenantId);
        Assert.Null(service.DepartmentId);
    }

    [Fact]
    public void ThePortalHeaderIsIgnored()
    {
        var context = HttpContexts.Authenticated(PortalClaims(), CrmAuthenticationSchemes.Portal)
            .WithHeader(OrganisationScope.HeaderName, ScopeFixtures.DeptB1Guid.ToString());
        var service = ServiceFor(context);

        Assert.True(service.Scope.IsPortalCustomer);
        Assert.False(service.Scope.ActiveScopeRejected);
    }

    [Theory]
    [InlineData(CrmClaimTypes.PortalCustomerId)]
    [InlineData(CrmClaimTypes.PortalContactId)]
    [InlineData(CrmClaimTypes.PortalAccountAdmin)]
    [InlineData(CrmClaimTypes.PortalBranchId)]
    [InlineData(CrmClaimTypes.PortalDepartmentId)]
    public void AMissingPortalClaimGivesTheNoneScope(string claimType)
    {
        var claims = PortalClaims(list => list.RemoveAll(c => c.Type == claimType));

        var service = ServiceFor(HttpContexts.Authenticated(claims, CrmAuthenticationSchemes.Portal));

        Assert.Same(OrganisationScope.None, service.Scope);
    }

    [Theory]
    [InlineData(CrmClaimTypes.PortalCustomerId, "garbage")]
    [InlineData(CrmClaimTypes.PortalContactId, "garbage")]
    [InlineData(CrmClaimTypes.PortalAccountAdmin, "maybe")]
    [InlineData(CrmClaimTypes.PortalBranchId, "garbage")]
    [InlineData(CrmClaimTypes.PortalDepartmentId, "garbage")]
    [InlineData(CrmClaimTypes.PortalCustomerId, "00000000-0000-0000-0000-000000000000")]
    [InlineData(CrmClaimTypes.PortalContactId, "00000000-0000-0000-0000-000000000000")]
    public void AMalformedOrEmptyPortalClaimGivesTheNoneScope(string claimType, string value)
    {
        var claims = PortalClaims(list =>
        {
            list.RemoveAll(c => c.Type == claimType);
            list.Add(new Claim(claimType, value));
        });

        var service = ServiceFor(HttpContexts.Authenticated(claims, CrmAuthenticationSchemes.Portal));

        Assert.Same(OrganisationScope.None, service.Scope);
    }

    [Fact]
    public void ANoneTenantOnAPortalPrincipalGivesTheNoneScope()
    {
        var claims = PortalClaims(list =>
        {
            list.RemoveAll(c => c.Type == CrmClaimTypes.TenantId);
            list.Add(new Claim(CrmClaimTypes.TenantId, Guid.Empty.ToString()));
        });

        var service = ServiceFor(HttpContexts.Authenticated(claims, CrmAuthenticationSchemes.Portal));

        Assert.Same(OrganisationScope.None, service.Scope);
    }

    [Fact]
    public void APrincipalWithBothAStaffAndAPortalIdentityGetsNoScope()
    {
        var portal = new ClaimsIdentity(PortalClaims(), CrmAuthenticationSchemes.Portal);
        var staff = new ClaimsIdentity(StaffClaims(A1), CrmAuthenticationSchemes.Staff);
        var context = new DefaultHttpContext { User = new ClaimsPrincipal([portal, staff]) };

        var service = ServiceFor(context);

        Assert.Same(OrganisationScope.None, service.Scope);
    }

    // --- caching ------------------------------------------------------------------------------------------

    [Fact]
    public void TheScopeIsCachedPerPrincipalInstance()
    {
        var service = Staff(null, A1);

        Assert.Same(service.Scope, service.Scope);
    }

    [Fact]
    public void TheScopeIsResolvedAgainWhenTheUserIsReplaced()
    {
        var context = new DefaultHttpContext();
        var service = ServiceFor(context);

        var before = service.Scope;
        context.User = new ClaimsPrincipal(new ClaimsIdentity(StaffClaims(A1), CrmAuthenticationSchemes.Staff));
        var after = service.Scope;

        Assert.Same(OrganisationScope.None, before);
        Assert.NotSame(before, after);
        Assert.Equal(ScopeFixtures.TenantA, after.Tenant);
    }
}
