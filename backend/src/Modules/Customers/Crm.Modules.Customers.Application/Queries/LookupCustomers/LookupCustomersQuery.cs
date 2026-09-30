using Crm.BuildingBlocks.Application.Authorization;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Customers.Application.Queries.LookupCustomers;

/// <summary>The ticket customer picker: active customers only, at most <paramref name="Take"/>.</summary>
[RequirePermission(CrmPermissions.CustomersRead)]
public sealed record LookupCustomersQuery(string? Search, int Take = 10) : IRequest<Result<IReadOnlyList<CustomerLookupDto>>>;
