using Crm.BuildingBlocks.Application.Authorization;
using Crm.Modules.Customers.Contracts;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Customers.Application.Queries.GetCustomerNames;

/// <summary>Behind ICustomersModuleApi.GetCustomerNamesAsync (Story 20 inbox). Scoped like every customer read.</summary>
[RequirePermission(CrmPermissions.CustomersRead)]
public sealed record GetCustomerNamesQuery(IReadOnlyList<Guid> CustomerIds) : IRequest<Result<IReadOnlyList<CustomerNameSummary>>>;
