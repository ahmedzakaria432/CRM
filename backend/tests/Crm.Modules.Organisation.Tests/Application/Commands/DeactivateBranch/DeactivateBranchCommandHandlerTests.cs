using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Authorization;
using Crm.Modules.Organisation.Application;
using Crm.Modules.Organisation.Application.Commands.DeactivateBranch;
using Crm.Modules.Organisation.Domain;
using Crm.Modules.Organisation.Tests.Support;
using Crm.SharedKernel.Results;
using Crm.SharedKernel.Scoping;

namespace Crm.Modules.Organisation.Tests.Application.Commands.DeactivateBranch;

public sealed class DeactivateBranchCommandHandlerTests
{
    private readonly IRepository<Branch, BranchId> _repository = Substitute.For<IRepository<Branch, BranchId>>();
    private readonly IOrganisationUnitOfWork _unitOfWork = Substitute.For<IOrganisationUnitOfWork>();

    private DeactivateBranchCommandHandler Handler() => new(_repository, _unitOfWork);

    [Fact]
    public async Task SuccessDeactivatesTheBranch()
    {
        Branch branch = Branches.New(id: Ids.BranchA1);
        _repository.Holds<Branch, BranchId>(branch);

        Result<BranchDto> result = await Handler().Handle(new DeactivateBranchCommand(Ids.BranchA1.Value, Ids.RowVersion()), Stubs.Ct);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.IsActive);
        Assert.False(branch.IsActive);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TheRowVersionIsAppliedBeforeSaving()
    {
        Branch branch = Branches.New(id: Ids.BranchA1);
        _repository.Holds<Branch, BranchId>(branch);
        byte[] rowVersion = Ids.RowVersion();

        await Handler().Handle(new DeactivateBranchCommand(Ids.BranchA1.Value, rowVersion), Stubs.Ct);

        Received.InOrder(() =>
        {
            _repository.SetOriginalRowVersion(branch, rowVersion);
            _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task AnAlreadyInactiveBranchThrowsAndIsNotSaved()
    {
        Branch branch = Branches.New(id: Ids.BranchA1, active: false);
        _repository.Holds<Branch, BranchId>(branch);

        await Expect.DomainCode(
            () => Handler().Handle(new DeactivateBranchCommand(Ids.BranchA1.Value, Ids.RowVersion()), Stubs.Ct),
            "Organisation.BranchAlreadyInactive");
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AMissingBranchIsNotFound()
    {
        Result<BranchDto> result = await Handler().Handle(new DeactivateBranchCommand(Guid.NewGuid(), Ids.RowVersion()), Stubs.Ct);

        Expect.Failure(result, "Organisation.BranchNotFound", ErrorType.NotFound);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void TheCommandRequiresTheAdminOrganisationPermission()
    {
        Assert.Equal(CrmPermissions.AdminOrganisation, Stubs.PermissionOf(typeof(DeactivateBranchCommand)));
    }
}
