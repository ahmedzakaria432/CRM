using Crm.BuildingBlocks.Application.Authorization;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Organisation.Application.Queries.ListBranches;

[RequirePermission(CrmPermissions.AdminOrganisation)]
public sealed record ListBranchesQuery(bool ActiveOnly) : IRequest<Result<IReadOnlyList<BranchDto>>>;
