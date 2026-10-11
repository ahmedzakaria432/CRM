using Crm.Modules.Organisation.Application.Specifications;
using Crm.Modules.Organisation.Domain;
using Crm.Modules.Organisation.Tests.Support;

namespace Crm.Modules.Organisation.Tests.Application.Specifications;

public sealed class BranchByCodeSpecificationTests
{
    [Fact]
    public void MatchesByNormalisedCode()
    {
        Branch riyadh = Branches.New(code: "RYD");
        Branch jeddah = Branches.New(code: "JED");

        var specification = new BranchByCodeSpecification(OrganisationCode.Create(" ryd "));

        Assert.Equal([riyadh], Stubs.Matching(specification, riyadh, jeddah));
    }

    [Fact]
    public void MatchesNothingWhenNoBranchHasTheCode()
    {
        Assert.Empty(Stubs.Matching(new BranchByCodeSpecification(OrganisationCode.Create("ZZ")), Branches.New(code: "RYD")));
    }

    [Fact]
    public void IsReadOnly()
    {
        Assert.True(new BranchByCodeSpecification(OrganisationCode.Create("RYD")).AsNoTracking);
    }
}
