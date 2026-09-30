using Crm.BuildingBlocks.Application.Authorization;
using Crm.Modules.Customers.Contracts;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Customers.Application.Queries.GetPortalContact;

/// <summary>In-process portal sign-in/refresh lookup (Identity, through ICustomersModuleApi); no HTTP endpoint exposes it.</summary>
[AllowAnonymousRequest]
public sealed record GetPortalContactQuery(Guid ContactId) : IRequest<Result<PortalContactMatch>>;
