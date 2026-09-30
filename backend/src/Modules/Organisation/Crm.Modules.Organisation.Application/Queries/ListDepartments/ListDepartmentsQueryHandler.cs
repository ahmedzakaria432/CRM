using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Organisation.Application.Specifications;
using Crm.Modules.Organisation.Domain;
using Crm.SharedKernel.Results;
using Crm.SharedKernel.Scoping;
using MediatR;

namespace Crm.Modules.Organisation.Application.Queries.ListDepartments;

public sealed class ListDepartmentsQueryHandler(
    IRepository<Department, DepartmentId> departmentRepository,
    IRepository<Branch, BranchId> branchRepository)
    : IRequestHandler<ListDepartmentsQuery, Result<IReadOnlyList<DepartmentDto>>>
{
    public async Task<Result<IReadOnlyList<DepartmentDto>>> Handle(ListDepartmentsQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<Department> departments = await departmentRepository.ListAsync(
            new DepartmentsSpecification(request.BranchId, request.ActiveOnly), cancellationToken);

        if (request.ActiveOnly)
        {
            IReadOnlyList<Branch> activeBranches = await branchRepository.ListAsync(new BranchesSpecification(true), cancellationToken);
            HashSet<Guid> activeBranchIds = activeBranches.Select(b => b.Id.Value).ToHashSet();
            departments = [.. departments.Where(d => activeBranchIds.Contains(d.BranchId.Value))];
        }

        return Result.Success<IReadOnlyList<DepartmentDto>>(departments.Select(DepartmentDto.FromDomain).ToList());
    }
}
