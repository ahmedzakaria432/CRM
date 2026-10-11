using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Specifications;
using Crm.Modules.Organisation.Application;
using Crm.Modules.Organisation.Application.Commands.CreateDepartment;
using Crm.Modules.Organisation.Application.Specifications;
using Crm.Modules.Organisation.Domain;
using Crm.Modules.Organisation.Tests.Support;
using Crm.SharedKernel.Results;
using Crm.SharedKernel.Scoping;

namespace Crm.Modules.Organisation.Tests.Application.Commands.CreateDepartment;

public sealed class CreateDepartmentCommandHandlerTests
{
    private readonly IRepository<Branch, BranchId> _branches = Substitute.For<IRepository<Branch, BranchId>>();
    private readonly IRepository<Department, DepartmentId> _departments = Substitute.For<IRepository<Department, DepartmentId>>();
    private readonly IOrganisationUnitOfWork _unitOfWork = Substitute.For<IOrganisationUnitOfWork>();
    private readonly Branch _branch = Branches.New(Ids.TenantA, id: Ids.BranchA1);

    private CreateDepartmentCommandHandler Handler() => new(_branches, _departments, _unitOfWork);

    private CreateDepartmentCommand Command(string code = "care") => new(_branch.Id.Value, code, "خدمة", "Care");

    [Fact]
    public async Task SuccessCopiesTenantAndBranchFromTheBranch()
    {
        _branches.Holds<Branch, BranchId>(_branch);
        _departments.CountsAs<Department, DepartmentId>(0);

        Result<DepartmentDto> result = await Handler().Handle(Command(), Stubs.Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(Ids.BranchA1.Value, result.Value.BranchId);
        Assert.Equal("CARE", result.Value.Code);
        Assert.True(result.Value.IsActive);
        _departments.Received(1).Add(Arg.Is<Department>(d => d.TenantId == Ids.TenantA && d.BranchId == Ids.BranchA1));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AMissingBranchIsNotFound()
    {
        Result<DepartmentDto> result = await Handler().Handle(Command(), Stubs.Ct);

        Expect.Failure(result, "Organisation.BranchNotFound", ErrorType.NotFound);
        _departments.DidNotReceive().Add(Arg.Any<Department>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ATakenCodeIsAConflictNamingTheNormalisedCode()
    {
        _branches.Holds<Branch, BranchId>(_branch);
        _departments.CountsAs<Department, DepartmentId>(1);

        Result<DepartmentDto> result = await Handler().Handle(Command(" care "), Stubs.Ct);

        Expect.Failure(result, "Organisation.DepartmentCodeTaken", ErrorType.Conflict);
        Assert.Contains("'CARE'", result.Error.Description, StringComparison.Ordinal);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TheDuplicateCheckUsesTheDepartmentByCodeSpecification()
    {
        _branches.Holds<Branch, BranchId>(_branch);
        _departments.CountsAs<Department, DepartmentId>(0);

        await Handler().Handle(Command(), Stubs.Ct);

        await _departments.Received(1).CountAsync(Arg.Is<Specification<Department>>(s => s is DepartmentByCodeSpecification), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnInactiveBranchThrowsADomainException()
    {
        Branch inactive = Branches.New(Ids.TenantA, code: "OLD", active: false, id: Ids.BranchA2);
        _branches.Holds<Branch, BranchId>(inactive);
        _departments.CountsAs<Department, DepartmentId>(0);

        await Expect.DomainCode(
            () => Handler().Handle(new CreateDepartmentCommand(inactive.Id.Value, "care", "خ", "C"), Stubs.Ct),
            "Organisation.BranchInactive");
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TheBranchIsLookedUpBeforeTheCodeIsChecked()
    {
        await Handler().Handle(Command(), Stubs.Ct);

        await _departments.DidNotReceive().CountAsync(Arg.Any<Specification<Department>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnInvalidCodeThrows()
    {
        _branches.Holds<Branch, BranchId>(_branch);

        await Expect.DomainCode(() => Handler().Handle(Command("a_b"), Stubs.Ct), "Organisation.CodeInvalid");
    }

    [Fact]
    public void TheCommandRequiresTheAdminOrganisationPermission()
    {
        Assert.Equal(CrmPermissions.AdminOrganisation, Stubs.PermissionOf(typeof(CreateDepartmentCommand)));
    }
}
