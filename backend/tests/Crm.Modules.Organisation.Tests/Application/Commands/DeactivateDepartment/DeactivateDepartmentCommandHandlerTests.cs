using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Authorization;
using Crm.Modules.Organisation.Application;
using Crm.Modules.Organisation.Application.Commands.DeactivateDepartment;
using Crm.Modules.Organisation.Domain;
using Crm.Modules.Organisation.Tests.Support;
using Crm.SharedKernel.Results;
using Crm.SharedKernel.Scoping;

namespace Crm.Modules.Organisation.Tests.Application.Commands.DeactivateDepartment;

public sealed class DeactivateDepartmentCommandHandlerTests
{
    private readonly IRepository<Department, DepartmentId> _repository = Substitute.For<IRepository<Department, DepartmentId>>();
    private readonly IOrganisationUnitOfWork _unitOfWork = Substitute.For<IOrganisationUnitOfWork>();

    private DeactivateDepartmentCommandHandler Handler() => new(_repository, _unitOfWork);

    [Fact]
    public async Task SuccessDeactivatesTheDepartment()
    {
        Department department = Departments.New(Branches.New(), id: Ids.DeptA1);
        _repository.Holds<Department, DepartmentId>(department);

        Result<DepartmentDto> result = await Handler().Handle(new DeactivateDepartmentCommand(Ids.DeptA1.Value, Ids.RowVersion()), Stubs.Ct);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.IsActive);
        Assert.False(department.IsActive);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TheRowVersionIsAppliedBeforeSaving()
    {
        Department department = Departments.New(Branches.New(), id: Ids.DeptA1);
        _repository.Holds<Department, DepartmentId>(department);
        byte[] rowVersion = Ids.RowVersion();

        await Handler().Handle(new DeactivateDepartmentCommand(Ids.DeptA1.Value, rowVersion), Stubs.Ct);

        Received.InOrder(() =>
        {
            _repository.SetOriginalRowVersion(department, rowVersion);
            _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task AnAlreadyInactiveDepartmentThrowsAndIsNotSaved()
    {
        Department department = Departments.New(Branches.New(), active: false, id: Ids.DeptA1);
        _repository.Holds<Department, DepartmentId>(department);

        await Expect.DomainCode(
            () => Handler().Handle(new DeactivateDepartmentCommand(Ids.DeptA1.Value, Ids.RowVersion()), Stubs.Ct),
            "Organisation.DepartmentAlreadyInactive");
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AMissingDepartmentIsNotFound()
    {
        Result<DepartmentDto> result = await Handler().Handle(new DeactivateDepartmentCommand(Guid.NewGuid(), Ids.RowVersion()), Stubs.Ct);

        Expect.Failure(result, "Organisation.DepartmentNotFound", ErrorType.NotFound);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void TheCommandRequiresTheAdminOrganisationPermission()
    {
        Assert.Equal(CrmPermissions.AdminOrganisation, Stubs.PermissionOf(typeof(DeactivateDepartmentCommand)));
    }
}
