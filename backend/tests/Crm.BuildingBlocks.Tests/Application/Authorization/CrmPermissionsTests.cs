using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Authorization;

namespace Crm.BuildingBlocks.Tests.Application.Authorization;

public sealed class CrmPermissionsTests
{
    [Fact]
    public void StaffHasTheEightStaffPermissions()
    {
        Assert.Equal(
            [
                "tickets.read", "tickets.write", "tickets.assign", "customers.read", "customers.write",
                "admin.organisation", "admin.users", "admin.branding",
            ],
            CrmPermissions.Staff);
    }

    [Fact]
    public void AllIsStaffPlusThePortalPermissions()
    {
        Assert.Equal([.. CrmPermissions.Staff, CrmPermissions.PortalAccount, CrmPermissions.PortalTickets], CrmPermissions.All);
        Assert.Equal(10, CrmPermissions.All.Count);
        Assert.Equal(CrmPermissions.All.Count, CrmPermissions.All.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void PermissionNamesAreStable()
    {
        Assert.Equal("portal.tickets", CrmPermissions.PortalTickets);
        Assert.Equal("portal.account", CrmPermissions.PortalAccount);
    }

    [Fact]
    public void AnAgentReadsAndWritesTicketsAndCustomersButCannotAssignOrAdminister()
    {
        var permissions = CrmPermissions.ForRoles([CrmRoles.Agent]);

        Assert.Equal(
            [CrmPermissions.CustomersRead, CrmPermissions.CustomersWrite, CrmPermissions.TicketsRead, CrmPermissions.TicketsWrite],
            permissions);
        Assert.DoesNotContain(CrmPermissions.TicketsAssign, permissions);
        Assert.DoesNotContain(CrmPermissions.AdminUsers, permissions);
    }

    [Fact]
    public void ASupervisorIsAnAgentPlusAssign()
    {
        var supervisor = CrmPermissions.ForRoles([CrmRoles.Supervisor]);
        var agent = CrmPermissions.ForRoles([CrmRoles.Agent]);

        Assert.Equal(agent.Append(CrmPermissions.TicketsAssign).Order(StringComparer.Ordinal), supervisor);
        Assert.DoesNotContain(CrmPermissions.AdminOrganisation, supervisor);
    }

    [Fact]
    public void AnAdministratorHoldsEveryStaffPermission()
    {
        var permissions = CrmPermissions.ForRoles([CrmRoles.Administrator]);

        Assert.Equal(CrmPermissions.Staff.Order(StringComparer.Ordinal), permissions);
    }

    [Fact]
    public void AnAdministratorNeverHoldsAPortalPermission()
    {
        var permissions = CrmPermissions.ForRoles([CrmRoles.Administrator]);

        Assert.DoesNotContain(CrmPermissions.PortalTickets, permissions);
        Assert.DoesNotContain(CrmPermissions.PortalAccount, permissions);
    }

    [Fact]
    public void APortalCustomerHoldsBothPortalPermissionsAndNoStaffPermission()
    {
        var permissions = CrmPermissions.ForRoles([CrmRoles.PortalCustomer]);

        Assert.Equal([CrmPermissions.PortalAccount, CrmPermissions.PortalTickets], permissions);
        Assert.Empty(permissions.Intersect(CrmPermissions.Staff));
    }

    [Fact]
    public void RolesUnionDistinctAndOrdinalSorted()
    {
        var permissions = CrmPermissions.ForRoles([CrmRoles.Agent, CrmRoles.Supervisor, CrmRoles.Agent]);

        Assert.Equal(permissions.Distinct(StringComparer.Ordinal).Count(), permissions.Count);
        Assert.Equal(permissions.Order(StringComparer.Ordinal), permissions);
        Assert.Contains(CrmPermissions.TicketsAssign, permissions);
    }

    [Fact]
    public void AnUnknownRoleGrantsNothing()
    {
        Assert.Empty(CrmPermissions.ForRoles(["Janitor"]));
        Assert.Equal(CrmPermissions.ForRoles([CrmRoles.Agent]), CrmPermissions.ForRoles(["Janitor", CrmRoles.Agent]));
    }

    [Fact]
    public void RoleNamesAreCaseSensitive()
    {
        Assert.Empty(CrmPermissions.ForRoles(["agent"]));
    }

    [Fact]
    public void NoRolesGrantNothing()
    {
        Assert.Empty(CrmPermissions.ForRoles([]));
    }

    [Fact]
    public void EveryCataloguedPermissionIsKnown()
    {
        foreach (string permission in CrmPermissions.All)
        {
            Assert.True(CrmPermissions.IsKnown(permission), permission);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("Tickets.Read")]
    [InlineData("tickets.READ")]
    [InlineData("tickets.delete")]
    [InlineData("tickets.read ")]
    public void AnythingElseIsNotKnown(string permission)
    {
        Assert.False(CrmPermissions.IsKnown(permission));
    }
}
