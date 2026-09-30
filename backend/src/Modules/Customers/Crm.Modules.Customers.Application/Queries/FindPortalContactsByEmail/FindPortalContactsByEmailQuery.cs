using Crm.BuildingBlocks.Application.Authorization;
using Crm.Modules.Customers.Contracts;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Customers.Application.Queries.FindPortalContactsByEmail;

/// <summary>In-process portal registration lookup (Identity, through ICustomersModuleApi); no HTTP endpoint exposes it.</summary>
[AllowAnonymousRequest]
public sealed record FindPortalContactsByEmailQuery(string Email) : IRequest<Result<IReadOnlyList<PortalContactMatch>>>;
