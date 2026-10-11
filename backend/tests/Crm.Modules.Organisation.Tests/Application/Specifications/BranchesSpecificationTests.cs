using Crm.Modules.Organisation.Application.Specifications;
using Crm.Modules.Organisation.Domain;
using Crm.Modules.Organisation.Tests.Support;

namespace Crm.Modules.Organisation.Tests.Application.Specifications;

public sealed class BranchesSpecificationTests
{
    private readonly Branch _jeddah = Branches.New(code: "JED");
    private readonly Branch _riyadh = Branches.New(code: "RYD");
    private readonly Branch _dammam = Branches.New(code: "DMM", active: false);

    [Fact]
    public void WithoutTheFlagEveryBranchIsKeptOrderedByCode()
    {
        List<Branch> result = Stubs.Matching(new BranchesSpecification(false), _jeddah, _riyadh, _dammam);

        Assert.Equal([_dammam, _jeddah, _riyadh], result);
    }

    [Fact]
    public void ActiveOnlyKeepsActiveBranches()
    {
        List<Branch> result = Stubs.Matching(new BranchesSpecification(true), _jeddah, _riyadh, _dammam);

        Assert.Equal([_jeddah, _riyadh], result);
    }

    [Fact]
    public void OrdersByTheCodeProperty()
    {
        var specification = new BranchesSpecification(false);

        Assert.NotNull(specification.OrderBy);
        Assert.Equal(_riyadh.Code, specification.OrderBy.Compile()(_riyadh));
    }

    [Fact]
    public void IsReadOnly()
    {
        Assert.True(new BranchesSpecification(false).AsNoTracking);
    }
}
