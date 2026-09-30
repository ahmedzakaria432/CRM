using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Organisation.Contracts;
using Crm.Modules.Organisation.Domain;
using Crm.SharedKernel.Results;
using Crm.SharedKernel.Scoping;
using MediatR;

namespace Crm.Modules.Organisation.Application.Queries.GetDepartmentReference;

public sealed class GetDepartmentReferenceQueryHandler(IRepository<Department, DepartmentId> departments)
    : IRequestHandler<GetDepartmentReferenceQuery, Result<DepartmentReference>>
{
    public async Task<Result<DepartmentReference>> Handle(GetDepartmentReferenceQuery request, CancellationToken cancellationToken)
    {
        Department? department = await departments.GetByIdAsync(new DepartmentId(request.DepartmentId), cancellationToken);
        if (department is null)
        {
            return Result.Failure<DepartmentReference>(
                Error.NotFound("Organisation.DepartmentNotFound", $"Department '{request.DepartmentId}' was not found."));
        }

        return Result.Success(new DepartmentReference(
            department.Id.Value, department.TenantId.Value, department.BranchId.Value, department.Code.Value,
            department.Name.Arabic, department.Name.English, department.IsActive));
    }
}
