using Crm.BuildingBlocks.Application.Authorization;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Organisation.Application.Queries.IsDepartmentMember;

/// <summary>In-process only (IOrganisationModuleApi); no HTTP endpoint.</summary>
[RequireAuthenticatedUser]
public sealed record IsDepartmentMemberQuery(Guid UserId, Guid DepartmentId) : IRequest<Result<bool>>;
