using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Identity.Contracts;
using Crm.Modules.Organisation.Contracts;
using Crm.SharedKernel.Results;
using Crm.SharedKernel.Scoping;
using MediatR;

namespace Crm.Modules.Tickets.Application.Queries.ListTicketAgents;

public sealed class ListTicketAgentsQueryHandler(
    ICurrentUserService currentUser, IOrganisationModuleApi organisation, IIdentityModuleApi identity)
    : IRequestHandler<ListTicketAgentsQuery, Result<IReadOnlyList<TicketAgentDto>>>
{
    public async Task<Result<IReadOnlyList<TicketAgentDto>>> Handle(ListTicketAgentsQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<DepartmentId> inScope = currentUser.Scope.Departments;
        List<Guid> departments;
        if (request.DepartmentId is { } requested)
        {
            if (!inScope.Contains(new DepartmentId(requested)))
            {
                return Result.Failure<IReadOnlyList<TicketAgentDto>>(TicketsErrors.DepartmentOutOfScope());
            }

            departments = [requested];
        }
        else
        {
            departments = [.. inScope.Select(d => d.Value)];
        }

        Result<IReadOnlyList<DepartmentMember>> members = await organisation.GetDepartmentMembersAsync(departments, cancellationToken);
        if (members.IsFailure)
        {
            return Result.Failure<IReadOnlyList<TicketAgentDto>>(members.Error);
        }

        Dictionary<Guid, List<Guid>> departmentsByUser = members.Value
            .GroupBy(m => m.UserId)
            .ToDictionary(g => g.Key, g => g.Select(m => m.DepartmentId).Distinct().ToList());

        // GetStaffUsersQueryValidator rejects more than 200 ids per call, so a scope with more members lists the first 200.
        Result<IReadOnlyList<StaffUserSummary>> users = await identity.GetStaffUsersAsync([.. departmentsByUser.Keys.Take(200)], cancellationToken);
        if (users.IsFailure)
        {
            return Result.Failure<IReadOnlyList<TicketAgentDto>>(users.Error);
        }

        return Result.Success<IReadOnlyList<TicketAgentDto>>(
        [
            .. users.Value
                .Where(u => u.IsActive)
                .OrderBy(u => u.DisplayName, StringComparer.CurrentCulture)
                .Select(u => new TicketAgentDto(u.Id, u.DisplayName, departmentsByUser[u.Id])),
        ]);
    }
}
