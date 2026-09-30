using Crm.Modules.Customers.Domain;

namespace Crm.Modules.Customers.Application.Commands.CreateCustomer;

internal static class DuplicateCandidates
{
    public static DuplicateCandidateDto From(Customer customer, IReadOnlyCollection<string> emails, IReadOnlyCollection<string> phones) =>
        new(
            customer.Id.Value,
            customer.Number.Value,
            CustomerNameDto.FromDomain(customer.DisplayName),
            customer.Status.ToString(),
            [.. customer.Contacts.Select(c => c.Email?.Value).OfType<string>().Where(emails.Contains)],
            [.. customer.Contacts.Select(c => c.Phone?.Value).OfType<string>().Where(phones.Contains)]);
}
