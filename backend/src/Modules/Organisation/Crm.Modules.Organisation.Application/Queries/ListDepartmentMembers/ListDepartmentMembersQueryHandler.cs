using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Organisation.Application.Specifications;
using Crm.Modules.Organisation.Contracts;
using Crm.Modules.Organisation.Domain;
using Crm.SharedKernel.Results;
using Crm.SharedKernel.Scoping;
using MediatR;

namespace Crm.Modules.Organisation.Application.Queries.ListDepartmentMembers;

public sealed class ListDepartmentMembersQueryHandler(IRepository<Membership, MembershipId> memberships)
    : IRequestHandler<ListDepartmentMembersQuery, Result<IReadOnlyList<DepartmentMember>>>
{
    public async Task<Result<IReadOnlyList<DepartmentMember>>> Handle(ListDepartmentMembersQuery request, CancellationToken cancellationToken)
    {
        if (request.DepartmentIds.Count == 0)
        {
            return Result.Success<IReadOnlyList<DepartmentMember>>([]);
        }

        // Membership is ITenantScoped + ISoftDeletable: the tenant and soft-delete filters apply automatically.
        IReadOnlyList<Membership> found = await memberships.ListAsync(
            new MembershipsOfDepartmentsSpecification([.. request.DepartmentIds.Distinct().Select(id => new DepartmentId(id))]),
            cancellationToken);

        return Result.Success<IReadOnlyList<DepartmentMember>>(
            [.. found.Select(m => new DepartmentMember(m.UserId, m.DepartmentId.Value, m.Role.ToString()))]);
    }
}
