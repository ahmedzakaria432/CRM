using Crm.BuildingBlocks.Application.Authorization;
using Crm.Modules.Identity.Contracts;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Identity.Application.Queries.GetStaffUsers;

/// <summary>In-process only (IIdentityModuleApi); no HTTP endpoint.</summary>
[RequireAuthenticatedUser]
public sealed record GetStaffUsersQuery(IReadOnlyList<Guid> UserIds) : IRequest<Result<IReadOnlyList<StaffUserSummary>>>;
