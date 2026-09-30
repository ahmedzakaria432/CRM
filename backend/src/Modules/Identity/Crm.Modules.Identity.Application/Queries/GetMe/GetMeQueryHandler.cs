using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Authorization;
using Crm.Modules.Identity.Domain;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Identity.Application.Queries.GetMe;

public sealed class GetMeQueryHandler(IRepository<User, UserId> users, ICurrentUserService currentUser)
    : IRequestHandler<GetMeQuery, Result<MeDto>>
{
    public async Task<Result<MeDto>> Handle(GetMeQuery request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(currentUser.UserId, out Guid userId))
        {
            return Result.Failure<MeDto>(IdentityErrors.Unauthenticated());
        }

        User? user = await users.GetByIdAsync(new UserId(userId), cancellationToken);

        // A deactivated user's still-unexpired access token dies here, not at its expiry.
        if (user is null || !user.IsActive)
        {
            return Result.Failure<MeDto>(IdentityErrors.Unauthenticated());
        }

        OrganisationScope scope = currentUser.Scope;

        return Result.Success(new MeDto(
            user.Id.Value,
            user.Email.Value,
            user.DisplayName.Value,
            user.PreferredLocale.Value,
            user.TenantId.Value,
            scope.ActiveDepartment?.Value,
            [.. scope.Memberships.Select(m => new MeMembershipDto(m.DepartmentId.Value, m.BranchId.Value, m.Role))],
            scope.EffectiveRoles,
            CrmPermissions.ForRoles(scope.EffectiveRoles)));
    }
}
