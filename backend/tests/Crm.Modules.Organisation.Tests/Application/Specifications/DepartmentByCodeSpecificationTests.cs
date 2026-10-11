using Crm.Modules.Organisation.Application.Specifications;
using Crm.Modules.Organisation.Domain;
using Crm.Modules.Organisation.Tests.Support;

namespace Crm.Modules.Organisation.Tests.Application.Specifications;

public sealed class DepartmentByCodeSpecificationTests
{
    private readonly Branch _branch = Branches.New();

    [Fact]
    public void MatchesByNormalisedCode()
    {
        Department care = Departments.New(_branch, "CARE");
        Department billing = Departments.New(_branch, "BILL");

        var specification = new DepartmentByCodeSpecification(OrganisationCode.Create("care"));

        Assert.Equal([care], Stubs.Matching(specification, care, billing));
    }

    [Fact]
    public void IsReadOnly()
    {
        Assert.True(new DepartmentByCodeSpecification(OrganisationCode.Create("CARE")).AsNoTracking);
    }
}
