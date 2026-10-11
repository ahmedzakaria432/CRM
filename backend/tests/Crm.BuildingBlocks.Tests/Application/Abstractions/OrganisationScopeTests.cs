using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Tests.Support;
using Crm.SharedKernel.Scoping;

namespace Crm.BuildingBlocks.Tests.Application.Abstractions;

public sealed class OrganisationScopeTests
{
    private static readonly ScopeMembership AgentA1 = Scopes.Member(ScopeFixtures.DeptA1, ScopeFixtures.BranchA1, CrmRoles.Agent);
    private static readonly ScopeMembership AdminA2 = Scopes.Member(ScopeFixtures.DeptA2, ScopeFixtures.BranchA2, CrmRoles.Administrator);
    private static readonly ScopeMembership SupervisorA2 = Scopes.Member(ScopeFixtures.DeptA2, ScopeFixtures.BranchA2, CrmRoles.Supervisor);

    // --- no active scope ----------------------------------------------------------------------------------

    [Fact]
    public void WithoutAnActiveScopeDepartmentsAreEveryMembershipDepartmentSortedByValue()
    {
        var scope = Scopes.Agent(AdminA2, AgentA1);

        Assert.Equal([ScopeFixtures.DeptA1, ScopeFixtures.DeptA2], scope.Departments);
    }

    [Fact]
    public void WithoutAnActiveScopeBranchesAreEveryMembershipBranchSortedByValue()
    {
        var scope = Scopes.Agent(AdminA2, AgentA1);

        Assert.Equal([ScopeFixtures.BranchA1, ScopeFixtures.BranchA2], scope.Branches);
    }

    [Fact]
    public void DepartmentsAndBranchesAreDistinct()
    {
        var scope = Scopes.Agent(AgentA1, Scopes.Member(ScopeFixtures.DeptA1, ScopeFixtures.BranchA1, CrmRoles.Supervisor));

        Assert.Single(scope.Departments);
        Assert.Single(scope.Branches);
    }

    [Fact]
    public void RolesAreDistinctAndOrdinalSorted()
    {
        var scope = Scopes.Agent(SupervisorA2, AgentA1, AdminA2, Scopes.Member(ScopeFixtures.DeptA1, ScopeFixtures.BranchA1, CrmRoles.Agent));

        Assert.Equal([CrmRoles.Administrator, CrmRoles.Agent, CrmRoles.Supervisor], scope.Roles);
    }

    [Fact]
    public void WithoutAnActiveScopeEffectiveRolesAreAllRoles()
    {
        var scope = Scopes.Agent(AgentA1, AdminA2);

        Assert.Equal([CrmRoles.Administrator, CrmRoles.Agent], scope.EffectiveRoles);
    }

    [Fact]
    public void MembershipsAreKeptAsGiven()
    {
        var scope = Scopes.Agent(AgentA1, AdminA2);

        Assert.Equal([AgentA1, AdminA2], scope.Memberships);
        Assert.Equal(ScopeFixtures.TenantA, scope.Tenant);
        Assert.Null(scope.ActiveDepartment);
        Assert.False(scope.ActiveScopeRejected);
        Assert.False(scope.IsUnrestricted);
        Assert.False(scope.IsPortalCustomer);
        Assert.Null(scope.PortalCustomer);
    }

    // --- narrowed -----------------------------------------------------------------------------------------

    [Fact]
    public void NarrowedDepartmentsIsTheActiveDepartmentAlone()
    {
        var scope = Scopes.Narrowed([AgentA1, AdminA2], ScopeFixtures.DeptA2);

        Assert.Equal([ScopeFixtures.DeptA2], scope.Departments);
        Assert.Equal(ScopeFixtures.DeptA2, scope.ActiveDepartment);
    }

    [Fact]
    public void NarrowedBranchesAreThoseOfTheActiveDepartmentOnly()
    {
        var scope = Scopes.Narrowed([AgentA1, AdminA2], ScopeFixtures.DeptA2);

        Assert.Equal([ScopeFixtures.BranchA2], scope.Branches);
    }

    [Fact]
    public void NarrowedBranchesCoverEveryBranchOfTheActiveDepartment()
    {
        var scope = Scopes.Narrowed(
            [
                Scopes.Member(ScopeFixtures.DeptA1, ScopeFixtures.BranchA2),
                Scopes.Member(ScopeFixtures.DeptA1, ScopeFixtures.BranchA1),
                AdminA2,
            ],
            ScopeFixtures.DeptA1);

        Assert.Equal([ScopeFixtures.BranchA1, ScopeFixtures.BranchA2], scope.Branches);
    }

    [Fact]
    public void NarrowedEffectiveRolesAreOnlyThoseOfTheActiveDepartmentWhileRolesListsAll()
    {
        var scope = Scopes.Narrowed([AgentA1, AdminA2], ScopeFixtures.DeptA1);

        Assert.Equal([CrmRoles.Agent], scope.EffectiveRoles);
        Assert.Equal([CrmRoles.Administrator, CrmRoles.Agent], scope.Roles);
    }

    // --- rejected -----------------------------------------------------------------------------------------

    [Fact]
    public void RejectedScopeHasNoDepartmentsBranchesOrEffectiveRoles()
    {
        var scope = Scopes.Rejected([AgentA1, AdminA2]);

        Assert.True(scope.ActiveScopeRejected);
        Assert.Empty(scope.Departments);
        Assert.Empty(scope.Branches);
        Assert.Empty(scope.EffectiveRoles);
    }

    [Fact]
    public void RejectedScopeStillListsTheCallersRoles()
    {
        var scope = Scopes.Rejected([AgentA1, AdminA2]);

        Assert.Equal([CrmRoles.Administrator, CrmRoles.Agent], scope.Roles);
    }

    [Fact]
    public void RejectedScopeAllowsNoScopedRow()
    {
        var scope = Scopes.Rejected([AgentA1]);

        Assert.False(scope.Allows(new OrganisationScopedRow(ScopeFixtures.TenantA, ScopeFixtures.BranchA1, ScopeFixtures.DeptA1)));
    }

    // --- unrestricted and none ----------------------------------------------------------------------------

    [Fact]
    public void UnrestrictedAllowsAnyRow()
    {
        Assert.True(OrganisationScope.Unrestricted.IsUnrestricted);
        Assert.True(OrganisationScope.Unrestricted.Allows(new TenantScopedRow(ScopeFixtures.TenantB)));
        Assert.True(OrganisationScope.Unrestricted.Allows(
            new OrganisationScopedRow(ScopeFixtures.TenantB, ScopeFixtures.BranchB1, ScopeFixtures.DeptB1)));
    }

    [Fact]
    public void UnrestrictedHasNoTenantAndNoDepartments()
    {
        Assert.Equal(TenantId.None, OrganisationScope.Unrestricted.Tenant);
        Assert.Empty(OrganisationScope.Unrestricted.Departments);
        Assert.False(OrganisationScope.Unrestricted.IsPortalCustomer);
    }

    [Fact]
    public void NoneIsNotUnrestrictedAndAllowsNoTenantRow()
    {
        Assert.False(OrganisationScope.None.IsUnrestricted);
        Assert.False(OrganisationScope.None.Allows(new TenantScopedRow(ScopeFixtures.TenantA)));
        Assert.False(OrganisationScope.None.Allows(
            new OrganisationScopedRow(ScopeFixtures.TenantA, ScopeFixtures.BranchA1, ScopeFixtures.DeptA1)));
    }

    // --- Allows -------------------------------------------------------------------------------------------

    [Fact]
    public void ARowOfAnotherTenantIsNeverAllowed()
    {
        var scope = Scopes.Agent(AgentA1);

        Assert.False(scope.Allows(new TenantScopedRow(ScopeFixtures.TenantB)));
        Assert.False(scope.Allows(new OrganisationScopedRow(ScopeFixtures.TenantB, ScopeFixtures.BranchA1, ScopeFixtures.DeptA1)));
    }

    [Fact]
    public void ATenantScopedRowOfTheCallersTenantIsAllowed()
    {
        var scope = Scopes.Agent(AgentA1);

        Assert.True(scope.Allows(new TenantScopedRow(ScopeFixtures.TenantA)));
    }

    [Fact]
    public void ATenantScopedRowIsAllowedEvenWithNoMemberships()
    {
        var scope = Scopes.Agent();

        Assert.True(scope.Allows(new TenantScopedRow(ScopeFixtures.TenantA)));
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void AnOrganisationScopedRowNeedsBothItsBranchAndItsDepartmentInScope(bool branchInScope, bool departmentInScope, bool expected)
    {
        var scope = Scopes.AgentIn(ScopeFixtures.DeptA1, ScopeFixtures.BranchA1);
        var branch = branchInScope ? ScopeFixtures.BranchA1 : ScopeFixtures.BranchA2;
        var department = departmentInScope ? ScopeFixtures.DeptA1 : ScopeFixtures.DeptA2;

        Assert.Equal(expected, scope.Allows(new OrganisationScopedRow(ScopeFixtures.TenantA, branch, department)));
    }

    [Fact]
    public void TheRuleIsColumnWiseSoAnUnpairedBranchAndDepartmentBothInScopeAreAllowed()
    {
        // Pins the C# twin of the SQL rule: branch and department are each tested against the scope's sets, not as a
        // pair. A real row always carries a consistent pair, so the distinction only matters for a malformed row.
        var scope = Scopes.Agent(AgentA1, AdminA2);

        Assert.True(scope.Allows(new OrganisationScopedRow(ScopeFixtures.TenantA, ScopeFixtures.BranchA2, ScopeFixtures.DeptA1)));
    }

    [Fact]
    public void ANarrowedScopeRefusesARowOfAnotherDepartmentTheCallerBelongsTo()
    {
        var scope = Scopes.Narrowed([AgentA1, AdminA2], ScopeFixtures.DeptA1);

        Assert.True(scope.Allows(new OrganisationScopedRow(ScopeFixtures.TenantA, ScopeFixtures.BranchA1, ScopeFixtures.DeptA1)));
        Assert.False(scope.Allows(new OrganisationScopedRow(ScopeFixtures.TenantA, ScopeFixtures.BranchA2, ScopeFixtures.DeptA2)));
    }

    [Fact]
    public void AllowsRejectsANullRow()
    {
        var scope = Scopes.Agent(AgentA1);

        NullArguments.AssertThrowsArgumentNull(scope.Allows, new object?[] { null });
    }

    [Fact]
    public void AStaffScopeJudgesAPortalOwnedRowByItsDepartmentAndBranch()
    {
        var scope = Scopes.Agent(AgentA1);

        Assert.True(scope.Allows(new PortalOwnedRow(ScopeFixtures.TenantA, ScopeFixtures.BranchA1, ScopeFixtures.DeptA1, Scopes.PortalCustomerGuid)));
        Assert.False(scope.Allows(new PortalOwnedRow(ScopeFixtures.TenantA, ScopeFixtures.BranchA2, ScopeFixtures.DeptA2, Scopes.PortalCustomerGuid)));
    }

    // --- portal customer ----------------------------------------------------------------------------------

    [Fact]
    public void APortalCustomerReachesItsOwnCustomersRow()
    {
        var scope = Scopes.Portal(ScopeFixtures.TenantA, Scopes.PortalCustomerGuid);

        Assert.True(scope.Allows(new PortalOwnedRow(ScopeFixtures.TenantA, ScopeFixtures.BranchA2, ScopeFixtures.DeptA2, Scopes.PortalCustomerGuid)));
    }

    [Fact]
    public void APortalCustomerNeverReachesAnotherCustomersRow()
    {
        var scope = Scopes.Portal(ScopeFixtures.TenantA, Scopes.PortalCustomerGuid);

        Assert.False(scope.Allows(new PortalOwnedRow(ScopeFixtures.TenantA, ScopeFixtures.BranchA1, ScopeFixtures.DeptA1, Guid.NewGuid())));
    }

    [Fact]
    public void APortalCustomerNeverReachesARowOfAnotherTenant()
    {
        var scope = Scopes.Portal(ScopeFixtures.TenantA, Scopes.PortalCustomerGuid);

        Assert.False(scope.Allows(new PortalOwnedRow(ScopeFixtures.TenantB, ScopeFixtures.BranchB1, ScopeFixtures.DeptB1, Scopes.PortalCustomerGuid)));
        Assert.False(scope.Allows(new TenantScopedRow(ScopeFixtures.TenantB)));
    }

    [Fact]
    public void APortalCustomerReachesTenantScopedRowsOfItsTenant()
    {
        var scope = Scopes.Portal(ScopeFixtures.TenantA, Scopes.PortalCustomerGuid);

        Assert.True(scope.Allows(new TenantScopedRow(ScopeFixtures.TenantA)));
    }

    [Fact]
    public void APortalCustomerNeverReachesAnOperationalRowThatIsNotCustomerOwned()
    {
        var scope = Scopes.Portal(ScopeFixtures.TenantA, Scopes.PortalCustomerGuid);

        Assert.False(scope.Allows(new OrganisationScopedRow(ScopeFixtures.TenantA, ScopeFixtures.BranchA1, ScopeFixtures.DeptA1)));
    }

    [Fact]
    public void ForPortalCustomerBuildsAPortalPrincipal()
    {
        var scope = Scopes.Portal(ScopeFixtures.TenantA, Scopes.PortalCustomerGuid);

        Assert.True(scope.IsPortalCustomer);
        Assert.False(scope.IsUnrestricted);
        Assert.Equal(ScopeFixtures.TenantA, scope.Tenant);
        Assert.Equal([CrmRoles.PortalCustomer], scope.Roles);
        Assert.Equal([CrmRoles.PortalCustomer], scope.EffectiveRoles);
        Assert.Empty(scope.Departments);
        Assert.Empty(scope.Branches);
        Assert.Empty(scope.Memberships);
        Assert.Null(scope.ActiveDepartment);
        Assert.False(scope.ActiveScopeRejected);
    }

    [Fact]
    public void ForPortalCustomerKeepsTheGrant()
    {
        var grant = new PortalCustomerGrant(Scopes.PortalCustomerGuid, Scopes.PortalContactGuid, true, ScopeFixtures.BranchA1, ScopeFixtures.DeptA1);

        var scope = OrganisationScope.ForPortalCustomer(ScopeFixtures.TenantA, grant);

        Assert.Same(grant, scope.PortalCustomer);
    }

    [Fact]
    public void ForPortalCustomerRefusesTheNoneTenant()
    {
        var grant = new PortalCustomerGrant(Scopes.PortalCustomerGuid, Scopes.PortalContactGuid, false, ScopeFixtures.BranchA1, ScopeFixtures.DeptA1);

        Assert.Throws<ArgumentException>(() => OrganisationScope.ForPortalCustomer(TenantId.None, grant));
    }

    [Fact]
    public void ForPortalCustomerRefusesAnEmptyCustomerId()
    {
        var grant = new PortalCustomerGrant(Guid.Empty, Scopes.PortalContactGuid, false, ScopeFixtures.BranchA1, ScopeFixtures.DeptA1);

        Assert.Throws<ArgumentException>(() => OrganisationScope.ForPortalCustomer(ScopeFixtures.TenantA, grant));
    }

    [Fact]
    public void ForPortalCustomerRefusesAnEmptyContactId()
    {
        var grant = new PortalCustomerGrant(Scopes.PortalCustomerGuid, Guid.Empty, false, ScopeFixtures.BranchA1, ScopeFixtures.DeptA1);

        Assert.Throws<ArgumentException>(() => OrganisationScope.ForPortalCustomer(ScopeFixtures.TenantA, grant));
    }

    [Fact]
    public void ForPortalCustomerRefusesNullArguments()
    {
        var grant = new PortalCustomerGrant(Scopes.PortalCustomerGuid, Scopes.PortalContactGuid, false, ScopeFixtures.BranchA1, ScopeFixtures.DeptA1);
        Func<TenantId, PortalCustomerGrant, OrganisationScope> factory = OrganisationScope.ForPortalCustomer;

        NullArguments.AssertThrowsArgumentNull(factory, null, grant);
        NullArguments.AssertThrowsArgumentNull(factory, ScopeFixtures.TenantA, null);
    }

    // --- ResolveWriteTarget -------------------------------------------------------------------------------

    [Fact]
    public void ARequestedDepartmentInScopeIsTheTargetWithItsOwnBranch()
    {
        var scope = Scopes.Agent(AgentA1, AdminA2);

        var target = scope.ResolveWriteTarget(ScopeFixtures.DeptA2Guid);

        Assert.Equal(new ScopeWriteTarget(ScopeFixtures.TenantA, ScopeFixtures.BranchA2, ScopeFixtures.DeptA2), target);
    }

    [Fact]
    public void ARequestedDepartmentOutsideScopeResolvesToNull()
    {
        var scope = Scopes.Agent(AgentA1);

        Assert.Null(scope.ResolveWriteTarget(ScopeFixtures.DeptB1Guid));
    }

    [Fact]
    public void ARequestedDepartmentWinsOverTheActiveDepartment()
    {
        var scope = Scopes.Narrowed([AgentA1, AdminA2], ScopeFixtures.DeptA1);

        // A narrowed scope has exactly one department, so a request for the other one is out of scope.
        Assert.Null(scope.ResolveWriteTarget(ScopeFixtures.DeptA2Guid));
        Assert.Equal(ScopeFixtures.DeptA1, scope.ResolveWriteTarget(ScopeFixtures.DeptA1Guid)?.DepartmentId);
    }

    [Fact]
    public void WithNoRequestTheActiveDepartmentIsTheTarget()
    {
        var scope = Scopes.Narrowed([AgentA1, AdminA2], ScopeFixtures.DeptA2);

        var target = scope.ResolveWriteTarget(null);

        Assert.Equal(new ScopeWriteTarget(ScopeFixtures.TenantA, ScopeFixtures.BranchA2, ScopeFixtures.DeptA2), target);
    }

    [Fact]
    public void WithNoRequestAndExactlyOneDepartmentThatDepartmentIsTheTarget()
    {
        var scope = Scopes.Agent(AgentA1);

        var target = scope.ResolveWriteTarget(null);

        Assert.Equal(new ScopeWriteTarget(ScopeFixtures.TenantA, ScopeFixtures.BranchA1, ScopeFixtures.DeptA1), target);
    }

    [Fact]
    public void WithNoRequestAndSeveralDepartmentsTheTargetIsAmbiguousSoNull()
    {
        var scope = Scopes.Agent(AgentA1, AdminA2);

        Assert.Null(scope.ResolveWriteTarget(null));
    }

    [Fact]
    public void ARejectedScopeResolvesNoTarget()
    {
        var scope = Scopes.Rejected([AgentA1]);

        Assert.Null(scope.ResolveWriteTarget(null));
        Assert.Null(scope.ResolveWriteTarget(ScopeFixtures.DeptA1Guid));
    }

    [Fact]
    public void AScopeWithNoMembershipsResolvesNoTarget()
    {
        Assert.Null(Scopes.Agent().ResolveWriteTarget(null));
        Assert.Null(OrganisationScope.Unrestricted.ResolveWriteTarget(ScopeFixtures.DeptA1Guid));
    }

    [Fact]
    public void ThePortalPrincipalResolvesNoTarget()
    {
        var scope = Scopes.Portal(ScopeFixtures.TenantA, Scopes.PortalCustomerGuid);

        Assert.Null(scope.ResolveWriteTarget(null));
        Assert.Null(scope.ResolveWriteTarget(ScopeFixtures.DeptA1Guid));
    }

    // --- constants and records ----------------------------------------------------------------------------

    [Fact]
    public void TheScopeHeaderAndAllKeywordAreStable()
    {
        Assert.Equal("X-Crm-Scope", OrganisationScope.HeaderName);
        Assert.Equal("all", OrganisationScope.AllDepartments);
    }

    [Fact]
    public void ScopeRecordsHaveValueEquality()
    {
        Assert.Equal(AgentA1, Scopes.Member(ScopeFixtures.DeptA1, ScopeFixtures.BranchA1, CrmRoles.Agent));
        Assert.NotEqual(AgentA1, Scopes.Member(ScopeFixtures.DeptA1, ScopeFixtures.BranchA1, CrmRoles.Supervisor));
        Assert.Equal(
            new ScopeWriteTarget(ScopeFixtures.TenantA, ScopeFixtures.BranchA1, ScopeFixtures.DeptA1),
            new ScopeWriteTarget(ScopeFixtures.TenantA, ScopeFixtures.BranchA1, ScopeFixtures.DeptA1));
    }
}
