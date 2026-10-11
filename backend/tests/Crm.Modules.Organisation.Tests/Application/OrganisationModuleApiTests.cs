using Crm.Modules.Organisation.Application;
using Crm.Modules.Organisation.Application.Queries.GetDepartmentReference;
using Crm.Modules.Organisation.Application.Queries.GetMembershipsOfUser;
using Crm.Modules.Organisation.Application.Queries.IsDepartmentMember;
using Crm.Modules.Organisation.Application.Queries.ListDepartmentMembers;
using Crm.Modules.Organisation.Application.Queries.ListDepartmentReferences;
using Crm.Modules.Organisation.Contracts;
using Crm.Modules.Organisation.Tests.Support;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Organisation.Tests.Application;

public sealed class OrganisationModuleApiTests
{
    private readonly ISender _sender = Substitute.For<ISender>();

    private OrganisationModuleApi Api() => new(_sender);

    [Fact]
    public async Task GetMembershipsSendsTheMatchingQueryAndReturnsItsResultUnchanged()
    {
        var userId = Guid.NewGuid();
        Result<UserMemberships> expected = Result.Success(new UserMemberships(userId, Ids.TenantA.Value, []));
        _sender.Send(Arg.Is<GetMembershipsOfUserQuery>(q => q.UserId == userId), Arg.Any<CancellationToken>()).Returns(Task.FromResult(expected));

        Result<UserMemberships> result = await Api().GetMembershipsAsync(userId, Stubs.Ct);

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task GetMembershipsPassesAFailureThrough()
    {
        Result<UserMemberships> failure = Result.Failure<UserMemberships>(Error.Forbidden("X.Y", "no"));
        _sender.Send(Arg.Any<GetMembershipsOfUserQuery>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(failure));

        Result<UserMemberships> result = await Api().GetMembershipsAsync(Guid.NewGuid(), Stubs.Ct);

        Assert.Same(failure, result);
    }

    [Fact]
    public async Task GetDepartmentSendsTheMatchingQuery()
    {
        var id = Guid.NewGuid();
        Result<DepartmentReference> expected = Result.Success(new DepartmentReference(id, Ids.TenantA.Value, Ids.BranchA1.Value, "C", "ع", "E", true));
        _sender.Send(Arg.Is<GetDepartmentReferenceQuery>(q => q.DepartmentId == id), Arg.Any<CancellationToken>()).Returns(Task.FromResult(expected));

        Result<DepartmentReference> result = await Api().GetDepartmentAsync(id, Stubs.Ct);

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task IsDepartmentMemberSendsTheMatchingQuery()
    {
        var user = Guid.NewGuid();
        var department = Guid.NewGuid();
        Result<bool> expected = Result.Success(true);
        _sender.Send(Arg.Is<IsDepartmentMemberQuery>(q => q.UserId == user && q.DepartmentId == department), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(expected));

        Result<bool> result = await Api().IsDepartmentMemberAsync(user, department, Stubs.Ct);

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task GetDepartmentMembersCopiesTheCollectionIntoAList()
    {
        Guid[] ids = [Guid.NewGuid(), Guid.NewGuid()];
        Result<IReadOnlyList<DepartmentMember>> expected = Result.Success<IReadOnlyList<DepartmentMember>>([]);
        _sender.Send(Arg.Is<ListDepartmentMembersQuery>(q => q.DepartmentIds.SequenceEqual(ids)), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(expected));
        var collection = new HashSet<Guid>(ids);

        Result<IReadOnlyList<DepartmentMember>> result = await Api().GetDepartmentMembersAsync(collection, Stubs.Ct);

        Assert.Same(expected, result);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ListDepartmentsForwardsTheActiveOnlyFlag(bool activeOnly)
    {
        Result<IReadOnlyList<DepartmentReference>> expected = Result.Success<IReadOnlyList<DepartmentReference>>([]);
        _sender.Send(Arg.Is<ListDepartmentReferencesQuery>(q => q.ActiveOnly == activeOnly), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(expected));

        Result<IReadOnlyList<DepartmentReference>> result = await Api().ListDepartmentsAsync(activeOnly, Stubs.Ct);

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task TheCancellationTokenIsForwarded()
    {
        using var source = new CancellationTokenSource();
        _sender.Send(Arg.Any<IsDepartmentMemberQuery>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(Result.Success(false)));

        await Api().IsDepartmentMemberAsync(Guid.NewGuid(), Guid.NewGuid(), source.Token);

        await _sender.Received(1).Send(Arg.Any<IsDepartmentMemberQuery>(), source.Token);
    }

    [Fact]
    public void ItImplementsTheContractsInterface()
    {
        Assert.IsAssignableFrom<IOrganisationModuleApi>(Api());
    }
}
