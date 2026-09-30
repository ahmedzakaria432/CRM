using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Organisation.Application.Specifications;
using Crm.Modules.Organisation.Domain;
using Crm.SharedKernel.Results;
using Crm.SharedKernel.Scoping;
using MediatR;

namespace Crm.Modules.Organisation.Application.Commands.CreateDepartment;

public sealed class CreateDepartmentCommandHandler(
    IRepository<Branch, BranchId> branchRepository,
    IRepository<Department, DepartmentId> departmentRepository,
    IOrganisationUnitOfWork unitOfWork)
    : IRequestHandler<CreateDepartmentCommand, Result<DepartmentDto>>
{
    public async Task<Result<DepartmentDto>> Handle(CreateDepartmentCommand request, CancellationToken cancellationToken)
    {
        Branch? branch = await branchRepository.GetByIdAsync(new BranchId(request.BranchId), cancellationToken);
        if (branch is null)
        {
            return Result.Failure<DepartmentDto>(Error.NotFound("Organisation.BranchNotFound", $"Branch '{request.BranchId}' was not found."));
        }

        OrganisationCode code = OrganisationCode.Create(request.Code);

        int taken = await departmentRepository.CountAsync(new DepartmentByCodeSpecification(code), cancellationToken);
        if (taken > 0)
        {
            return Result.Failure<DepartmentDto>(Error.Conflict("Organisation.DepartmentCodeTaken", $"Department code '{code.Value}' is already in use."));
        }

        // Department.Create throws DomainException (422 Organisation.BranchInactive) when the branch is
        // inactive - a domain invariant, not a Result failure (CLAUDE.md: guard clauses live in the domain).
        Department department = Department.Create(branch, code, LocalizedText.Create(request.NameAr, request.NameEn));
        departmentRepository.Add(department);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(DepartmentDto.FromDomain(department));
    }
}
