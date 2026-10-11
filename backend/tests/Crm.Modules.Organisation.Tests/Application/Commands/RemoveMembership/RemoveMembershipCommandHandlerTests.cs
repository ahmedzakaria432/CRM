using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Authorization;
using Crm.Modules.Organisation.Application;
using Crm.Modules.Organisation.Application.Commands.RemoveMembership;
using Crm.Modules.Organisation.Domain;
using Crm.Modules.Organisation.Tests.Support;
using Crm.SharedKernel.Results;

namespace Crm.Modules.Organisation.Tests.Application.Commands.RemoveMembership;

public sealed class RemoveMembershipCommandHandlerTests
{
    private readonly IRepository<Membership, MembershipId> _repository = Substitute.For<IRepository<Membership, MembershipId>>();
    private readonly IOrganisationUnitOfWork _unitOfWork = Substitute.For<IOrganisationUnitOfWork>();

    [Fact]
    public async Task SuccessRemovesThenSaves()
    {
        Membership membership = Memberships.New(Departments.New(Branches.New()));
        _repository.Holds<Membership, MembershipId>(membership);

        Result result = await new RemoveMembershipCommandHandler(_repository, _unitOfWork)
            .Handle(new RemoveMembershipCommand(membership.Id.Value), Stubs.Ct);

        Assert.True(result.IsSuccess);
        Received.InOrder(() =>
        {
            _repository.Remove(membership);
            _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task AMissingMembershipIsNotFound()
    {
        Result result = await new RemoveMembershipCommandHandler(_repository, _unitOfWork)
            .Handle(new RemoveMembershipCommand(Guid.NewGuid()), Stubs.Ct);

        Expect.Failure(result, "Organisation.MembershipNotFound", ErrorType.NotFound);
        _repository.DidNotReceive().Remove(Arg.Any<Membership>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NoRowVersionIsSetBecauseRemovalCarriesNone()
    {
        Membership membership = Memberships.New(Departments.New(Branches.New()));
        _repository.Holds<Membership, MembershipId>(membership);

        await new RemoveMembershipCommandHandler(_repository, _unitOfWork)
            .Handle(new RemoveMembershipCommand(membership.Id.Value), Stubs.Ct);

        _repository.DidNotReceive().SetOriginalRowVersion(Arg.Any<Membership>(), Arg.Any<byte[]>());
    }

    [Fact]
    public void TheCommandRequiresTheAdminOrganisationPermission()
    {
        Assert.Equal(CrmPermissions.AdminOrganisation, Stubs.PermissionOf(typeof(RemoveMembershipCommand)));
    }
}
