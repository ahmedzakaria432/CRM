using Crm.Modules.Organisation.Application.Specifications;
using Crm.Modules.Organisation.Domain;
using Crm.Modules.Organisation.Tests.Support;

namespace Crm.Modules.Organisation.Tests.Application.Specifications;

public sealed class DepartmentsSpecificationTests
{
    private readonly Department _care;
    private readonly Department _billing;
    private readonly Department _old;
    private readonly Department _jeddah;

    public DepartmentsSpecificationTests()
    {
        Branch riyadh = Branches.New(code: "RYD", id: Ids.BranchA1);
        Branch jeddah = Branches.New(code: "JED", id: Ids.BranchA2);
        _care = Departments.New(riyadh, "RYD-CARE");
        _billing = Departments.New(riyadh, "RYD-BILL");
        _old = Departments.New(riyadh, "RYD-OLD", active: false);
        _jeddah = Departments.New(jeddah, "JED-CARE");
    }

    private List<Department> Run(Guid? branch, bool activeOnly) =>
        Stubs.Matching(new DepartmentsSpecification(branch, activeOnly), _care, _billing, _old, _jeddah);

    [Fact]
    public void NoFilterKeepsEverythingOrderedByCode()
    {
        Assert.Equal([_jeddah, _billing, _care, _old], Run(null, false));
    }

    [Fact]
    public void TheBranchFilterKeepsOnlyThatBranch()
    {
        Assert.Equal([_billing, _care, _old], Run(Ids.BranchA1.Value, false));
    }

    [Fact]
    public void ActiveOnlyDropsInactiveDepartments()
    {
        Assert.Equal([_jeddah, _billing, _care], Run(null, true));
    }

    [Fact]
    public void TheBranchFilterAndActiveOnlyCombineInOnePredicate()
    {
        Assert.Equal([_billing, _care], Run(Ids.BranchA1.Value, true));
    }

    [Fact]
    public void AnUnknownBranchMatchesNothing()
    {
        Assert.Empty(Run(Guid.NewGuid(), false));
    }

    [Fact]
    public void IsReadOnly()
    {
        Assert.True(new DepartmentsSpecification(null, false).AsNoTracking);
    }
}
