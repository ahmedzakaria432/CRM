using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Customers.Application.Specifications;
using Crm.Modules.Customers.Contracts;
using Crm.Modules.Customers.Domain;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Customers.Application.Queries.FindPortalContactsByEmail;

public sealed class FindPortalContactsByEmailQueryHandler(IRepository<Customer, CustomerId> repository)
    : IRequestHandler<FindPortalContactsByEmailQuery, Result<IReadOnlyList<PortalContactMatch>>>
{
    public async Task<Result<IReadOnlyList<PortalContactMatch>>> Handle(FindPortalContactsByEmailQuery request, CancellationToken cancellationToken)
    {
        if (!EmailAddress.TryCreate(request.Email, out EmailAddress? email))
        {
            return Result.Success<IReadOnlyList<PortalContactMatch>>([]);
        }

        IReadOnlyList<Customer> customers = await repository.ListAsync(
            new PortalContactsSpecification(email.Value, null), cancellationToken);

        List<PortalContactMatch> matches = [];
        foreach (Customer customer in customers)
        {
            foreach (Contact contact in customer.Contacts.Where(c => c.HasPortalAccess && c.Email is not null && c.Email.Value == email.Value))
            {
                if (PortalContactMapper.ToMatch(customer, contact) is { } match)
                {
                    matches.Add(match);
                }
            }
        }

        return Result.Success<IReadOnlyList<PortalContactMatch>>(matches);
    }
}
