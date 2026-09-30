using Crm.BuildingBlocks.Application.Authorization;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Organisation.Application.Queries.ListMemberships;

[RequirePermission(CrmPermissions.AdminOrganisation)]
public sealed record ListMembershipsQuery(Guid? UserId, Guid? DepartmentId) : IRequest<Result<IReadOnlyList<MembershipDto>>>;
