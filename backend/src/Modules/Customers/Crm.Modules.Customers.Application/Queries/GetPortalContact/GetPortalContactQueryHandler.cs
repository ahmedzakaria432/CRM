using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Customers.Application.Specifications;
using Crm.Modules.Customers.Contracts;
using Crm.Modules.Customers.Domain;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Customers.Application.Queries.GetPortalContact;

public sealed class GetPortalContactQueryHandler(IRepository<Customer, CustomerId> repository)
    : IRequestHandler<GetPortalContactQuery, Result<PortalContactMatch>>
{
    public async Task<Result<PortalContactMatch>> Handle(GetPortalContactQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<Customer> customers = await repository.ListAsync(
            new PortalContactsSpecification(null, request.ContactId), cancellationToken);

        foreach (Customer customer in customers)
        {
            Contact? contact = customer.Contacts.FirstOrDefault(c => c.Id == request.ContactId && c.HasPortalAccess);
            if (contact is not null && PortalContactMapper.ToMatch(customer, contact) is { } match)
            {
                return Result.Success(match);
            }
        }

        return Result.Failure<PortalContactMatch>(CustomersErrors.PortalContactNotFound(request.ContactId));
    }
}
