using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Specifications;
using Crm.Modules.Organisation.Application;
using Crm.Modules.Organisation.Application.Commands.AddMembership;
using Crm.Modules.Organisation.Application.Specifications;
using Crm.Modules.Organisation.Domain;
using Crm.Modules.Organisation.Tests.Support;
using Crm.SharedKernel.Results;
using Crm.SharedKernel.Scoping;

namespace Crm.Modules.Organisation.Tests.Application.Commands.AddMembership;

public sealed class AddMembershipCommandHandlerTests
{
    private readonly IRepository<Department, DepartmentId> _departments = Substitute.For<IRepository<Department, DepartmentId>>();
    private readonly IRepository<Membership, MembershipId> _memberships = Substitute.For<IRepository<Membership, MembershipId>>();
    private readonly IOrganisationUnitOfWork _unitOfWork = Substitute.For<IOrganisationUnitOfWork>();
    private readonly Department _department = Departments.New(Branches.New(id: Ids.BranchA1), id: Ids.DeptA1);

    private AddMembershipCommandHandler Handler() => new(_departments, _memberships, _unitOfWork);

    private AddMembershipCommand Command(string role = "Agent") => new(Ids.User, _department.Id.Value, role);

    [Fact]
    public async Task SuccessAddsTheMembershipAndSavesOnce()
    {
        _departments.Holds<Department, DepartmentId>(_department);
        _memberships.CountsAs<Membership, MembershipId>(0);

        Result<MembershipDto> result = await Handler().Handle(Command("Supervisor"), Stubs.Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(Ids.User, result.Value.UserId);
        Assert.Equal(_department.Id.Value, result.Value.DepartmentId);
        Assert.Equal(Ids.BranchA1.Value, result.Value.BranchId);
        Assert.Equal("Supervisor", result.Value.Role);
        _memberships.Received(1).Add(Arg.Is<Membership>(m => m.UserId == Ids.User && m.Role == SystemRole.Supervisor));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("Administrator", SystemRole.Administrator)]
    [InlineData("Supervisor", SystemRole.Supervisor)]
    [InlineData("Agent", SystemRole.Agent)]
    public async Task EverySystemRoleMaps(string role, SystemRole expected)
    {
        _departments.Holds<Department, DepartmentId>(_department);
        _memberships.CountsAs<Membership, MembershipId>(0);

        Result<MembershipDto> result = await Handler().Handle(Command(role), Stubs.Ct);

        Assert.Equal(expected.ToString(), result.Value.Role);
    }

    [Fact]
    public async Task AMissingDepartmentIsNotFoundAndNothingIsSaved()
    {
        Result<MembershipDto> result = await Handler().Handle(Command(), Stubs.Ct);

        Expect.Failure(result, "Organisation.DepartmentNotFound", ErrorType.NotFound);
        _memberships.DidNotReceive().Add(Arg.Any<Membership>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ADuplicateActiveMembershipIsAConflict()
    {
        _departments.Holds<Department, DepartmentId>(_department);
        _memberships.CountsAs<Membership, MembershipId>(1);

        Result<MembershipDto> result = await Handler().Handle(Command(), Stubs.Ct);

        Expect.Failure(result, "Organisation.MembershipExists", ErrorType.Conflict);
        _memberships.DidNotReceive().Add(Arg.Any<Membership>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnInactiveDepartmentThrowsADomainExceptionRatherThanReturningAResult()
    {
        Department inactive = Departments.New(Branches.New(), active: false, id: Ids.DeptA2);
        _departments.Holds<Department, DepartmentId>(inactive);
        _memberships.CountsAs<Membership, MembershipId>(0);

        await Expect.DomainCode(
            () => Handler().Handle(new AddMembershipCommand(Ids.User, inactive.Id.Value, "Agent"), Stubs.Ct),
            "Organisation.DepartmentInactive");
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ARoleThatIsNotAnEnumNameThrowsBecauseTheValidatorIsTheGuard()
    {
        _departments.Holds<Department, DepartmentId>(_department);
        _memberships.CountsAs<Membership, MembershipId>(0);

        await Assert.ThrowsAsync<ArgumentException>(() => Handler().Handle(Command("agent"), Stubs.Ct));
    }

    [Fact]
    public async Task TheDuplicateCheckUsesTheActiveMembershipSpecificationForThatUserAndDepartment()
    {
        _departments.Holds<Department, DepartmentId>(_department);
        _memberships.CountsAs<Membership, MembershipId>(0);

        await Handler().Handle(Command(), Stubs.Ct);

        Specification<Membership> specification = Assert.Single(_memberships.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(IRepository<Membership, MembershipId>.CountAsync))
            .Select(c => Assert.IsAssignableFrom<Specification<Membership>>(c.GetArguments()[0])));
        Assert.IsType<ActiveMembershipSpecification>(specification);
        Membership same = Memberships.New(_department, Ids.User);
        Membership otherUser = Memberships.New(_department, Ids.OtherUser);
        Assert.Equal([same], Stubs.Matching(specification, same, otherUser));
    }

    [Fact]
    public void TheCommandRequiresTheAdminOrganisationPermission()
    {
        Assert.Equal(CrmPermissions.AdminOrganisation, Stubs.PermissionOf(typeof(AddMembershipCommand)));
    }
}
