using Crm.BuildingBlocks.Application.Authorization;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Organisation.Application.Queries.GetMyScope;

[RequireAuthenticatedUser]
public sealed record GetMyScopeQuery : IRequest<Result<MyScopeDto>>;
