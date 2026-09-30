using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Organisation.Application.Specifications;
using Crm.Modules.Organisation.Domain;
using Crm.SharedKernel.Results;
using Crm.SharedKernel.Scoping;
using MediatR;

namespace Crm.Modules.Organisation.Application.Queries.GetMyScope;

public sealed class GetMyScopeQueryHandler(
    ICurrentUserService currentUser,
    IRepository<Department, DepartmentId> departmentRepository,
    IRepository<Branch, BranchId> branchRepository)
    : IRequestHandler<GetMyScopeQuery, Result<MyScopeDto>>
{
    public async Task<Result<MyScopeDto>> Handle(GetMyScopeQuery request, CancellationToken cancellationToken)
    {
        OrganisationScope scope = currentUser.Scope;

        if (scope.Memberships.Count == 0)
        {
            return Result.Failure<MyScopeDto>(Error.Forbidden("Organisation.NoMemberships", "The caller has no department memberships."));
        }

        IReadOnlyList<DepartmentId> departmentIds = [.. scope.Memberships.Select(m => m.DepartmentId).Distinct()];
        IReadOnlyList<Department> departments = await departmentRepository.ListAsync(
            new DepartmentsByIdsSpecification(departmentIds), cancellationToken);

        IReadOnlyList<BranchId> branchIds = [.. departments.Select(d => d.BranchId).Distinct()];
        IReadOnlyList<Branch> branches = await branchRepository.ListAsync(new BranchesByIdsSpecification(branchIds), cancellationToken);

        Dictionary<Guid, Department> departmentsById = departments.ToDictionary(d => d.Id.Value);
        Dictionary<Guid, Branch> branchesById = branches.ToDictionary(b => b.Id.Value);

        List<MyDepartmentDto> myDepartments = [];
        foreach (ScopeMembership membership in scope.Memberships)
        {
            if (!departmentsById.TryGetValue(membership.DepartmentId.Value, out Department? department)
                || !branchesById.TryGetValue(membership.BranchId.Value, out Branch? branch))
            {
                continue; // a membership pointing at a department/branch that no longer exists - defensive only
            }

            myDepartments.Add(new MyDepartmentDto(
                department.Id.Value,
                department.Code.Value,
                LocalizedTextDto.FromDomain(department.Name),
                branch.Id.Value,
                LocalizedTextDto.FromDomain(branch.Name),
                membership.Role,
                department.IsActive && branch.IsActive));
        }

        var dto = new MyScopeDto(
            Guid.Parse(currentUser.UserId!),
            scope.Tenant.Value,
            scope.ActiveDepartment?.Value,
            scope.Roles,
            myDepartments);

        return Result.Success(dto);
    }
}
