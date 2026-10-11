using Crm.Modules.Organisation.Application.Specifications;
using Crm.Modules.Organisation.Domain;
using Crm.Modules.Organisation.Tests.Support;

namespace Crm.Modules.Organisation.Tests.Application.Specifications;

public sealed class ActiveMembershipSpecificationTests
{
    private readonly Department _care = Departments.New(Branches.New(), "CARE", id: Ids.DeptA1);
    private readonly Department _billing = Departments.New(Branches.New(code: "JED"), "BILL", id: Ids.DeptA2);

    [Fact]
    public void MatchesTheUserInTheDepartmentOnly()
    {
        Membership match = Memberships.New(_care, Ids.User);
        Membership otherUser = Memberships.New(_care, Ids.OtherUser);
        Membership otherDepartment = Memberships.New(_billing, Ids.User);

        var specification = new ActiveMembershipSpecification(Ids.User, Ids.DeptA1);

        Assert.Equal([match], Stubs.Matching(specification, match, otherUser, otherDepartment));
    }

    [Fact]
    public void IsReadOnlyAndKeepsBothFilters()
    {
        var specification = new ActiveMembershipSpecification(Ids.User, Ids.DeptA1);

        Assert.True(specification.AsNoTracking);
        Assert.False(specification.IgnoreSoftDeleteFilter);
        Assert.False(specification.IgnoreOrganisationScopeFilter);
    }
}
