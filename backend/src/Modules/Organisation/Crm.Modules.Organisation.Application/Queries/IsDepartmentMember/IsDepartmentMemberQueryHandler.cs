using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Organisation.Application.Specifications;
using Crm.Modules.Organisation.Domain;
using Crm.SharedKernel.Results;
using Crm.SharedKernel.Scoping;
using MediatR;

namespace Crm.Modules.Organisation.Application.Queries.IsDepartmentMember;

public sealed class IsDepartmentMemberQueryHandler(IRepository<Membership, MembershipId> memberships)
    : IRequestHandler<IsDepartmentMemberQuery, Result<bool>>
{
    public async Task<Result<bool>> Handle(IsDepartmentMemberQuery request, CancellationToken cancellationToken)
    {
        int count = await memberships.CountAsync(
            new ActiveMembershipSpecification(request.UserId, new DepartmentId(request.DepartmentId)), cancellationToken);

        return Result.Success(count > 0);
    }
}
