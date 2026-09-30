using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Organisation.Application.Specifications;
using Crm.Modules.Organisation.Contracts;
using Crm.Modules.Organisation.Domain;
using Crm.SharedKernel.Results;
using Crm.SharedKernel.Scoping;
using MediatR;

namespace Crm.Modules.Organisation.Application.Queries.ListDepartmentReferences;

public sealed class ListDepartmentReferencesQueryHandler(IRepository<Department, DepartmentId> departments)
    : IRequestHandler<ListDepartmentReferencesQuery, Result<IReadOnlyList<DepartmentReference>>>
{
    public async Task<Result<IReadOnlyList<DepartmentReference>>> Handle(ListDepartmentReferencesQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<Department> found = await departments.ListAsync(new DepartmentsSpecification(null, request.ActiveOnly), cancellationToken);

        return Result.Success<IReadOnlyList<DepartmentReference>>(
        [
            .. found.Select(d => new DepartmentReference(
                d.Id.Value, d.TenantId.Value, d.BranchId.Value, d.Code.Value, d.Name.Arabic, d.Name.English, d.IsActive)),
        ]);
    }
}
