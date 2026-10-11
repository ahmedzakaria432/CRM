using Crm.Modules.Organisation.Application.Specifications;
using Crm.Modules.Organisation.Domain;
using Crm.Modules.Organisation.Tests.Support;
using Crm.SharedKernel.Scoping;

namespace Crm.Modules.Organisation.Tests.Application.Specifications;

public sealed class MembershipsOfDepartmentsSpecificationTests
{
    private readonly Membership _care;
    private readonly Membership _billing;

    public MembershipsOfDepartmentsSpecificationTests()
    {
        Branch branch = Branches.New();
        _care = Memberships.New(Departments.New(branch, "CARE", id: Ids.DeptA1));
        _billing = Memberships.New(Departments.New(branch, "BILL", id: Ids.DeptA2), Ids.OtherUser);
    }

    [Fact]
    public void MatchesMembershipsOfTheListedDepartments()
    {
        IReadOnlyList<DepartmentId> ids = [Ids.DeptA2];

        Assert.Equal([_billing], Stubs.Matching(new MembershipsOfDepartmentsSpecification(ids), _care, _billing));
    }

    [Fact]
    public void AnEmptyListMatchesNothing()
    {
        Assert.Empty(Stubs.Matching(new MembershipsOfDepartmentsSpecification([]), _care, _billing));
    }

    [Fact]
    public void ANullListIsRejected()
    {
        Func<IReadOnlyList<DepartmentId>, MembershipsOfDepartmentsSpecification> create = ids => new MembershipsOfDepartmentsSpecification(ids);

        NullArguments.AssertThrowsArgumentNull(create, new object?[] { null });
    }

    [Fact]
    public void IsReadOnly()
    {
        Assert.True(new MembershipsOfDepartmentsSpecification([]).AsNoTracking);
    }
}
