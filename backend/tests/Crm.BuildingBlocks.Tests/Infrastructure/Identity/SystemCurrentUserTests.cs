using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Infrastructure.Identity;

namespace Crm.BuildingBlocks.Tests.Infrastructure.Identity;

public sealed class SystemCurrentUserTests
{
    [Fact]
    public void ThereIsOneSharedInstance()
    {
        Assert.Same(SystemCurrentUser.Instance, SystemCurrentUser.Instance);
    }

    [Fact]
    public void ItIsTheSystemWithAnUnrestrictedScope()
    {
        var user = SystemCurrentUser.Instance;

        Assert.Equal("system", user.UserIdOrSystem);
        Assert.Same(OrganisationScope.Unrestricted, user.Scope);
    }

    [Fact]
    public void ItCarriesNoIds()
    {
        var user = SystemCurrentUser.Instance;

        Assert.Null(user.UserId);
        Assert.Null(user.SessionId);
        Assert.Null(user.TenantId);
        Assert.Null(user.DepartmentId);
        Assert.Null(user.BranchId);
    }
}
