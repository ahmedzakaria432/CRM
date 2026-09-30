using Crm.BuildingBlocks.Application.Authorization;
using Crm.Modules.Customers.Contracts;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Customers.Application.Queries.FindContactsByEmail;

/// <summary>Behind ICustomersModuleApi.FindContactByEmailAsync. Runs under the caller's scope and customers.read - an
/// anonymous caller (Story 47 registration) needs its own reviewed path.</summary>
[RequirePermission(CrmPermissions.CustomersRead)]
public sealed record FindContactsByEmailQuery(string Email) : IRequest<Result<IReadOnlyList<CustomerContactMatch>>>;
