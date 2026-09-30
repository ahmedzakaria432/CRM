using Crm.BuildingBlocks.Application.Authorization;
using Crm.Modules.Organisation.Contracts;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Organisation.Application.Queries.GetMembershipsOfUser;

/// <summary>In-process sign-in lookup (Identity's sign-in and refresh, through IOrganisationModuleApi); no HTTP endpoint exposes it.</summary>
[AllowAnonymousRequest]
public sealed record GetMembershipsOfUserQuery(Guid UserId) : IRequest<Result<UserMemberships>>;
