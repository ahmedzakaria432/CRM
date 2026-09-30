using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Organisation.Application.Specifications;
using Crm.Modules.Organisation.Domain;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Organisation.Application.Queries.ListMemberships;

public sealed class ListMembershipsQueryHandler(IRepository<Membership, MembershipId> repository)
    : IRequestHandler<ListMembershipsQuery, Result<IReadOnlyList<MembershipDto>>>
{
    public async Task<Result<IReadOnlyList<MembershipDto>>> Handle(ListMembershipsQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<Membership> memberships = await repository.ListAsync(
            new MembershipsSpecification(request.UserId, request.DepartmentId), cancellationToken);

        return Result.Success<IReadOnlyList<MembershipDto>>(memberships.Select(MembershipDto.FromDomain).ToList());
    }
}
