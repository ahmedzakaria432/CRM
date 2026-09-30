using Crm.BuildingBlocks.Application.Authorization;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Tickets.Application.Queries.ListTicketAgents;

/// <summary>Active staff of one department (must be in the caller's active scope) or of every department in it.
/// Feeds the inbox assignee filter and the ticket's assign picker.</summary>
[RequirePermission(CrmPermissions.TicketsRead)]
public sealed record ListTicketAgentsQuery(Guid? DepartmentId) : IRequest<Result<IReadOnlyList<TicketAgentDto>>>;
