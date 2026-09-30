using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Organisation.Application.Specifications;
using Crm.Modules.Organisation.Contracts;
using Crm.Modules.Organisation.Domain;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Organisation.Application.Queries.GetMembershipsOfUser;

/// <summary>The only reader of MembershipsOfUserSpecification - the sign-in membership lookup, run while the
/// caller has no scope yet (the dev login handler, and Story 07's real sign-in).</summary>
public sealed class GetMembershipsOfUserQueryHandler(IRepository<Membership, MembershipId> repository)
    : IRequestHandler<GetMembershipsOfUserQuery, Result<UserMemberships>>
{
    public async Task<Result<UserMemberships>> Handle(GetMembershipsOfUserQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<Membership> memberships = await repository.ListAsync(
            new MembershipsOfUserSpecification(request.UserId), cancellationToken);

        if (memberships.Count == 0)
        {
            return Result.Success(new UserMemberships(request.UserId, Guid.Empty, []));
        }

        List<UserMembership> list = [.. memberships.Select(m =>
            new UserMembership(m.Id.Value, m.DepartmentId.Value, m.BranchId.Value, m.Role.ToString()))];

        return Result.Success(new UserMemberships(request.UserId, memberships[0].TenantId.Value, list));
    }
}
