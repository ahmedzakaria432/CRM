using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Authorization;
using Crm.Modules.Organisation.Application;
using Crm.Modules.Organisation.Application.Commands.RenameDepartment;
using Crm.Modules.Organisation.Domain;
using Crm.Modules.Organisation.Tests.Support;
using Crm.SharedKernel.Results;
using Crm.SharedKernel.Scoping;

namespace Crm.Modules.Organisation.Tests.Application.Commands.RenameDepartment;

public sealed class RenameDepartmentCommandHandlerTests
{
    private readonly IRepository<Department, DepartmentId> _repository = Substitute.For<IRepository<Department, DepartmentId>>();
    private readonly IOrganisationUnitOfWork _unitOfWork = Substitute.For<IOrganisationUnitOfWork>();
    private readonly Department _department = Departments.New(Branches.New(), id: Ids.DeptA1);

    private RenameDepartmentCommandHandler Handler() => new(_repository, _unitOfWork);

    [Fact]
    public async Task SuccessRenamesAndReturnsTheDto()
    {
        _repository.Holds<Department, DepartmentId>(_department);

        Result<DepartmentDto> result = await Handler().Handle(new RenameDepartmentCommand(Ids.DeptA1.Value, "فوترة", "Billing", Ids.RowVersion()), Stubs.Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal("Billing", result.Value.Name.En);
        Assert.Equal("Billing", _department.Name.English);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TheRowVersionIsSetBeforeSaving()
    {
        _repository.Holds<Department, DepartmentId>(_department);
        byte[] rowVersion = Ids.RowVersion();

        await Handler().Handle(new RenameDepartmentCommand(Ids.DeptA1.Value, "ف", "B", rowVersion), Stubs.Ct);

        Received.InOrder(() =>
        {
            _repository.SetOriginalRowVersion(_department, rowVersion);
            _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task AMissingDepartmentIsNotFound()
    {
        Result<DepartmentDto> result = await Handler().Handle(new RenameDepartmentCommand(Guid.NewGuid(), "ف", "B", Ids.RowVersion()), Stubs.Ct);

        Expect.Failure(result, "Organisation.DepartmentNotFound", ErrorType.NotFound);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ABlankNameThrows()
    {
        _repository.Holds<Department, DepartmentId>(_department);

        await Expect.DomainCode(
            () => Handler().Handle(new RenameDepartmentCommand(Ids.DeptA1.Value, "ف", " ", Ids.RowVersion()), Stubs.Ct),
            "Organisation.NameRequired");
    }

    [Fact]
    public void TheCommandRequiresTheAdminOrganisationPermission()
    {
        Assert.Equal(CrmPermissions.AdminOrganisation, Stubs.PermissionOf(typeof(RenameDepartmentCommand)));
    }
}
