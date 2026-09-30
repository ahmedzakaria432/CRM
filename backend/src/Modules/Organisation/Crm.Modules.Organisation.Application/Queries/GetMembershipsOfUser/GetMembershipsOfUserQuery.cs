using Crm.Modules.Organisation.Contracts;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Organisation.Application.Queries.GetMembershipsOfUser;

public sealed record GetMembershipsOfUserQuery(Guid UserId) : IRequest<Result<UserMemberships>>;
