using Crm.BuildingBlocks.Application.Authorization;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Identity.Application.Queries.GetMe;

[RequireAuthenticatedUser]
public sealed record GetMeQuery : IRequest<Result<MeDto>>;
