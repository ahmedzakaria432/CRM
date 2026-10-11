using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Authorization;
using Crm.Modules.Organisation.Application;
using Crm.Modules.Organisation.Application.Commands.RenameBranch;
using Crm.Modules.Organisation.Domain;
using Crm.Modules.Organisation.Tests.Support;
using Crm.SharedKernel.Results;
using Crm.SharedKernel.Scoping;

namespace Crm.Modules.Organisation.Tests.Application.Commands.RenameBranch;

public sealed class RenameBranchCommandHandlerTests
{
    private readonly IRepository<Branch, BranchId> _repository = Substitute.For<IRepository<Branch, BranchId>>();
    private readonly IOrganisationUnitOfWork _unitOfWork = Substitute.For<IOrganisationUnitOfWork>();
    private readonly Branch _branch = Branches.New(id: Ids.BranchA1);

    private RenameBranchCommandHandler Handler() => new(_repository, _unitOfWork);

    [Fact]
    public async Task SuccessRenamesAndReturnsTheDto()
    {
        _repository.Holds<Branch, BranchId>(_branch);

        Result<BranchDto> result = await Handler().Handle(new RenameBranchCommand(Ids.BranchA1.Value, "جدة", "Jeddah", Ids.RowVersion()), Stubs.Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal("Jeddah", result.Value.Name.En);
        Assert.Equal("Jeddah", _branch.Name.English);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TheRowVersionIsSetBeforeSaving()
    {
        _repository.Holds<Branch, BranchId>(_branch);
        byte[] rowVersion = Ids.RowVersion();

        await Handler().Handle(new RenameBranchCommand(Ids.BranchA1.Value, "جدة", "Jeddah", rowVersion), Stubs.Ct);

        Received.InOrder(() =>
        {
            _repository.SetOriginalRowVersion(_branch, rowVersion);
            _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task AMissingBranchIsNotFound()
    {
        Result<BranchDto> result = await Handler().Handle(new RenameBranchCommand(Guid.NewGuid(), "ج", "J", Ids.RowVersion()), Stubs.Ct);

        Expect.Failure(result, "Organisation.BranchNotFound", ErrorType.NotFound);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ABlankNameThrows()
    {
        _repository.Holds<Branch, BranchId>(_branch);

        await Expect.DomainCode(
            () => Handler().Handle(new RenameBranchCommand(Ids.BranchA1.Value, "", "J", Ids.RowVersion()), Stubs.Ct),
            "Organisation.NameRequired");
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void TheCommandRequiresTheAdminOrganisationPermission()
    {
        Assert.Equal(CrmPermissions.AdminOrganisation, Stubs.PermissionOf(typeof(RenameBranchCommand)));
    }
}
