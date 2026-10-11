using Crm.Modules.Organisation.Application.Specifications;
using Crm.Modules.Organisation.Domain;
using Crm.Modules.Organisation.Tests.Support;

namespace Crm.Modules.Organisation.Tests.Application.Specifications;

public sealed class MembershipsSpecificationTests
{
    private readonly Membership _userCare;
    private readonly Membership _userBilling;
    private readonly Membership _otherCare;

    public MembershipsSpecificationTests()
    {
        Branch branch = Branches.New();
        Department care = Departments.New(branch, "CARE", id: Ids.DeptA1);
        Department billing = Departments.New(branch, "BILL", id: Ids.DeptA2);
        _userCare = Memberships.New(care, Ids.User);
        _userBilling = Memberships.New(billing, Ids.User);
        _otherCare = Memberships.New(care, Ids.OtherUser);
    }

    private List<Membership> Run(Guid? user, Guid? department) =>
        Stubs.Matching(new MembershipsSpecification(user, department), _userCare, _userBilling, _otherCare);

    [Fact]
    public void NoFilterKeepsEverything()
    {
        Assert.Equal(3, Run(null, null).Count);
    }

    [Fact]
    public void TheUserFilterKeepsThatUser()
    {
        Assert.Equal([_userCare, _userBilling], Run(Ids.User, null));
    }

    [Fact]
    public void TheDepartmentFilterKeepsThatDepartment()
    {
        Assert.Equal([_userCare, _otherCare], Run(null, Ids.DeptA1.Value));
    }

    [Fact]
    public void BothFiltersCombine()
    {
        Assert.Equal([_userCare], Run(Ids.User, Ids.DeptA1.Value));
    }

    [Fact]
    public void IsReadOnlyAndKeepsBothFilters()
    {
        var specification = new MembershipsSpecification(null, null);

        Assert.True(specification.AsNoTracking);
        Assert.False(specification.IgnoreOrganisationScopeFilter);
        Assert.False(specification.IgnoreSoftDeleteFilter);
    }
}
