using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Authorization;
using Crm.Modules.Organisation.Application;
using Crm.Modules.Organisation.Application.Commands.ChangeMembershipRole;
using Crm.Modules.Organisation.Domain;
using Crm.Modules.Organisation.Tests.Support;
using Crm.SharedKernel.Results;

namespace Crm.Modules.Organisation.Tests.Application.Commands.ChangeMembershipRole;

public sealed class ChangeMembershipRoleCommandHandlerTests
{
    private readonly IRepository<Membership, MembershipId> _repository = Substitute.For<IRepository<Membership, MembershipId>>();
    private readonly IOrganisationUnitOfWork _unitOfWork = Substitute.For<IOrganisationUnitOfWork>();
    private readonly Membership _membership = Memberships.New(Departments.New(Branches.New()), role: SystemRole.Agent);

    private ChangeMembershipRoleCommandHandler Handler() => new(_repository, _unitOfWork);

    [Fact]
    public async Task SuccessChangesTheRoleAndReturnsTheDto()
    {
        _repository.Holds<Membership, MembershipId>(_membership);

        Result<MembershipDto> result = await Handler().Handle(
            new ChangeMembershipRoleCommand(_membership.Id.Value, "Supervisor", Ids.RowVersion()), Stubs.Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal("Supervisor", result.Value.Role);
        Assert.Equal(SystemRole.Supervisor, _membership.Role);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TheClientRowVersionIsSetBeforeSaving()
    {
        _repository.Holds<Membership, MembershipId>(_membership);
        byte[] rowVersion = Ids.RowVersion();

        await Handler().Handle(new ChangeMembershipRoleCommand(_membership.Id.Value, "Agent", rowVersion), Stubs.Ct);

        Received.InOrder(() =>
        {
            _repository.SetOriginalRowVersion(_membership, rowVersion);
            _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task AMissingMembershipIsNotFound()
    {
        Result<MembershipDto> result = await Handler().Handle(
            new ChangeMembershipRoleCommand(Guid.NewGuid(), "Agent", Ids.RowVersion()), Stubs.Ct);

        Expect.Failure(result, "Organisation.MembershipNotFound", ErrorType.NotFound);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ABadRoleStringThrowsBecauseTheValidatorIsTheGuard()
    {
        _repository.Holds<Membership, MembershipId>(_membership);

        await Assert.ThrowsAsync<ArgumentException>(() => Handler().Handle(
            new ChangeMembershipRoleCommand(_membership.Id.Value, "Owner", Ids.RowVersion()), Stubs.Ct));
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void TheCommandRequiresTheAdminOrganisationPermission()
    {
        Assert.Equal(CrmPermissions.AdminOrganisation, Stubs.PermissionOf(typeof(ChangeMembershipRoleCommand)));
    }
}
