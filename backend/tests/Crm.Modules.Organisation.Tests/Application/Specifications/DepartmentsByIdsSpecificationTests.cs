using Crm.Modules.Organisation.Application.Specifications;
using Crm.Modules.Organisation.Domain;
using Crm.Modules.Organisation.Tests.Support;
using Crm.SharedKernel.Scoping;

namespace Crm.Modules.Organisation.Tests.Application.Specifications;

public sealed class DepartmentsByIdsSpecificationTests
{
    private readonly Department _care;
    private readonly Department _billing;

    public DepartmentsByIdsSpecificationTests()
    {
        Branch branch = Branches.New();
        _care = Departments.New(branch, "CARE", id: Ids.DeptA1);
        _billing = Departments.New(branch, "BILL", id: Ids.DeptA2);
    }

    [Fact]
    public void MatchesTheListedIds()
    {
        IReadOnlyList<DepartmentId> ids = [Ids.DeptA1, Ids.DeptA2];

        Assert.Equal([_care, _billing], Stubs.Matching(new DepartmentsByIdsSpecification(ids), _care, _billing));
    }

    [Fact]
    public void AnEmptyListMatchesNothing()
    {
        Assert.Empty(Stubs.Matching(new DepartmentsByIdsSpecification([]), _care, _billing));
    }

    [Fact]
    public void IsReadOnly()
    {
        Assert.True(new DepartmentsByIdsSpecification([]).AsNoTracking);
    }
}
