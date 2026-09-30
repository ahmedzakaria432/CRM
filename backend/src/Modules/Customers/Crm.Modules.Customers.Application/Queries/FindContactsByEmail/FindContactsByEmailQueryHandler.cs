using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Customers.Application.Specifications;
using Crm.Modules.Customers.Contracts;
using Crm.Modules.Customers.Domain;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Customers.Application.Queries.FindContactsByEmail;

public sealed class FindContactsByEmailQueryHandler(IRepository<Customer, CustomerId> repository)
    : IRequestHandler<FindContactsByEmailQuery, Result<IReadOnlyList<CustomerContactMatch>>>
{
    public async Task<Result<IReadOnlyList<CustomerContactMatch>>> Handle(FindContactsByEmailQuery request, CancellationToken cancellationToken)
    {
        if (!EmailAddress.TryCreate(request.Email, out EmailAddress? email))
        {
            return Result.Success<IReadOnlyList<CustomerContactMatch>>([]);
        }

        IReadOnlyList<Customer> customers = await repository.ListAsync(
            new CustomersWithContactEmailSpecification(email.Value), cancellationToken);

        return Result.Success<IReadOnlyList<CustomerContactMatch>>(
        [
            .. customers.SelectMany(
                customer => customer.Contacts.Where(c => c.Email is not null && c.Email.Value == email.Value),
                (customer, contact) => new CustomerContactMatch(
                    customer.Id.Value, customer.Number.Value, customer.IsActive, contact.Id, contact.Name, email.Value,
                    contact.HasPortalAccess, customer.TenantId.Value, customer.BranchId.Value, customer.DepartmentId.Value)),
        ]);
    }
}
