using Crm.Modules.Organisation.Application.Specifications;
using Crm.Modules.Organisation.Domain;
using Crm.Modules.Organisation.Tests.Support;
using Crm.SharedKernel.Scoping;

namespace Crm.Modules.Organisation.Tests.Application.Specifications;

public sealed class BranchesByIdsSpecificationTests
{
    private readonly Branch _riyadh = Branches.New(code: "RYD", id: Ids.BranchA1);
    private readonly Branch _jeddah = Branches.New(code: "JED", id: Ids.BranchA2);

    [Fact]
    public void MatchesTheListedIds()
    {
        IReadOnlyList<BranchId> ids = [Ids.BranchA2];

        Assert.Equal([_jeddah], Stubs.Matching(new BranchesByIdsSpecification(ids), _riyadh, _jeddah));
    }

    [Fact]
    public void AnEmptyListMatchesNothing()
    {
        Assert.Empty(Stubs.Matching(new BranchesByIdsSpecification([]), _riyadh, _jeddah));
    }

    [Fact]
    public void IsReadOnly()
    {
        Assert.True(new BranchesByIdsSpecification([]).AsNoTracking);
    }
}
