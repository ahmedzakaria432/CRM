using Crm.Modules.Organisation.Application.Specifications;
using Crm.Modules.Organisation.Domain;
using Crm.Modules.Organisation.Tests.Support;

namespace Crm.Modules.Organisation.Tests.Application.Specifications;

public sealed class OrganisationByCodeSpecificationTests
{
    [Fact]
    public void MatchesByNormalisedCode()
    {
        var demo = Organisations.New("DEMO");
        var other = Organisations.New("OTHER");

        var specification = new OrganisationByCodeSpecification(OrganisationCode.Create(" demo"));

        Assert.Equal([demo], Stubs.Matching(specification, demo, other));
    }

    [Fact]
    public void IsReadOnly()
    {
        Assert.True(new OrganisationByCodeSpecification(OrganisationCode.Create("DEMO")).AsNoTracking);
    }
}
