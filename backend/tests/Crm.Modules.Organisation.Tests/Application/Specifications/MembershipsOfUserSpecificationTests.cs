using Crm.Modules.Organisation.Application.Specifications;
using Crm.Modules.Organisation.Domain;
using Crm.Modules.Organisation.Tests.Support;

namespace Crm.Modules.Organisation.Tests.Application.Specifications;

public sealed class MembershipsOfUserSpecificationTests
{
    [Fact]
    public void MatchesOnlyThatUsersMemberships()
    {
        Department care = Departments.New(Branches.New(), "CARE");
        Membership mine = Memberships.New(care, Ids.User);
        Membership theirs = Memberships.New(care, Ids.OtherUser);

        Assert.Equal([mine], Stubs.Matching(new MembershipsOfUserSpecification(Ids.User), mine, theirs));
    }

    [Fact]
    public void LiftsOnlyTheScopeFilterNeverSoftDelete()
    {
        var specification = new MembershipsOfUserSpecification(Ids.User);

        Assert.True(specification.IgnoreOrganisationScopeFilter);
        Assert.False(specification.IgnoreSoftDeleteFilter);
    }

    [Fact]
    public void IsReadOnly()
    {
        Assert.True(new MembershipsOfUserSpecification(Ids.User).AsNoTracking);
    }
}
