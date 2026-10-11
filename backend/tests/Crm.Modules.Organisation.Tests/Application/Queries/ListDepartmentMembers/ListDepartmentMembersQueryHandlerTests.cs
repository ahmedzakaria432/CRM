using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Specifications;
using Crm.Modules.Organisation.Application.Queries.ListDepartmentMembers;
using Crm.Modules.Organisation.Contracts;
using Crm.Modules.Organisation.Domain;
using Crm.Modules.Organisation.Tests.Support;
using Crm.SharedKernel.Results;

namespace Crm.Modules.Organisation.Tests.Application.Queries.ListDepartmentMembers;

public sealed class ListDepartmentMembersQueryHandlerTests
{
    private readonly IRepository<Membership, MembershipId> _repository = Substitute.For<IRepository<Membership, MembershipId>>();
    private readonly Branch _branch = Branches.New(id: Ids.BranchA1);
    private readonly Department _care;
    private readonly Department _billing;

    public ListDepartmentMembersQueryHandlerTests()
    {
        _care = Departments.New(_branch, "CARE", id: Ids.DeptA1);
        _billing = Departments.New(_branch, "BILL", id: Ids.DeptA2);
    }

    private Task<Result<IReadOnlyList<DepartmentMember>>> RunAsync(params Guid[] departments) =>
        new ListDepartmentMembersQueryHandler(_repository).Handle(new ListDepartmentMembersQuery(departments), Stubs.Ct);

    [Fact]
    public async Task NoDepartmentsIsAnEmptySuccessWithoutTouchingTheRepository()
    {
        Result<IReadOnlyList<DepartmentMember>> result = await RunAsync();

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
        Assert.Empty(_repository.ReceivedCalls());
    }

    [Fact]
    public async Task ReturnsTheMembersOfTheRequestedDepartmentsWithRoleStrings()
    {
        _repository.Lists<Membership, MembershipId>(
            Memberships.New(_care, Ids.User, SystemRole.Supervisor),
            Memberships.New(_billing, Ids.OtherUser, SystemRole.Administrator));

        Result<IReadOnlyList<DepartmentMember>> result = await RunAsync(Ids.DeptA1.Value, Ids.DeptA2.Value);

        Assert.Equal(2, result.Value.Count);
        Assert.Contains(new DepartmentMember(Ids.User, Ids.DeptA1.Value, "Supervisor"), result.Value);
        Assert.Contains(new DepartmentMember(Ids.OtherUser, Ids.DeptA2.Value, "Administrator"), result.Value);
    }

    [Fact]
    public async Task OnlyTheRequestedDepartmentsAreMatched()
    {
        _repository.Lists<Membership, MembershipId>(
            Memberships.New(_care, Ids.User),
            Memberships.New(_billing, Ids.OtherUser));

        Result<IReadOnlyList<DepartmentMember>> result = await RunAsync(Ids.DeptA2.Value);

        Assert.Equal(Ids.OtherUser, Assert.Single(result.Value).UserId);
    }

    [Fact]
    public async Task DuplicateDepartmentIdsAreCollapsedBeforeTheQuery()
    {
        _repository.Lists<Membership, MembershipId>(Memberships.New(_care, Ids.User));

        Result<IReadOnlyList<DepartmentMember>> result = await RunAsync(Ids.DeptA1.Value, Ids.DeptA1.Value);

        Assert.Single(result.Value);
        await _repository.Received(1).ListAsync(Arg.Any<Specification<Membership>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void TheQueryRequiresAnAuthenticatedUser()
    {
        Assert.True(Stubs.HasAttribute<RequireAuthenticatedUserAttribute>(typeof(ListDepartmentMembersQuery)));
    }
}
