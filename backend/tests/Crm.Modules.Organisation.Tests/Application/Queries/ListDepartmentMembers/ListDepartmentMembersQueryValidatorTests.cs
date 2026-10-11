using Crm.Modules.Organisation.Application.Queries.ListDepartmentMembers;
using Crm.Modules.Organisation.Tests.Support;

namespace Crm.Modules.Organisation.Tests.Application.Queries.ListDepartmentMembers;

public sealed class ListDepartmentMembersQueryValidatorTests
{
    private readonly ListDepartmentMembersQueryValidator _validator = new();

    private static ListDepartmentMembersQuery Query(int count) =>
        new([.. Enumerable.Range(0, count).Select(_ => Guid.NewGuid())]);

    [Fact]
    public void AnEmptyListIsValid()
    {
        Expect.Valid(_validator.Validate(Query(0)));
    }

    [Fact]
    public void OneHundredIdsAreValid()
    {
        Expect.Valid(_validator.Validate(Query(100)));
    }

    [Fact]
    public void OneHundredAndOneIdsAreInvalid()
    {
        Expect.Invalid(_validator.Validate(Query(101)), nameof(ListDepartmentMembersQuery.DepartmentIds));
    }

    [Fact]
    public void ANullListMakesTheValidatorThrowRatherThanFail()
    {
        // Pinned (Follow-up): NotNull does not stop the chain, so the Must predicate dereferences null. A literal null does
        // not compile warning-free for a non-nullable parameter, so build the query by reflection.
        var query = (ListDepartmentMembersQuery)typeof(ListDepartmentMembersQuery).GetConstructors()[0].Invoke([null]);

        Assert.Throws<NullReferenceException>(() => _validator.Validate(query));
    }
}
