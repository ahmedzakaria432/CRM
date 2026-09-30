using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Organisation.Application.Queries.GetMyScope;

public sealed record GetMyScopeQuery : IRequest<Result<MyScopeDto>>;
