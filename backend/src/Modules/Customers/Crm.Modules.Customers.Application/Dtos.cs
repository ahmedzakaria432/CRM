using Crm.Modules.Customers.Domain;

namespace Crm.Modules.Customers.Application;

// Primitives only: the one place in Application that touches domain types for mapping.
public sealed record CustomerNameDto(string Ar, string En)
{
    public static CustomerNameDto FromDomain(LocalizedText name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return new CustomerNameDto(name.Arabic, name.English);
    }
}

public sealed record ContactDto(Guid Id, string Name, string? JobTitle, string? Email, string? Phone, bool IsPrimary, bool HasPortalAccess)
{
    public static ContactDto FromDomain(Contact contact)
    {
        ArgumentNullException.ThrowIfNull(contact);
        return new ContactDto(
            contact.Id, contact.Name, contact.JobTitle, contact.Email?.Value, contact.Phone?.Value, contact.IsPrimary, contact.HasPortalAccess);
    }
}

public sealed record CustomerNoteDto(Guid Id, string Body, bool IsPinned, Guid AuthorId, DateTimeOffset CreatedAtUtc, DateTimeOffset? PinnedAtUtc)
{
    public static CustomerNoteDto FromDomain(CustomerNote note)
    {
        ArgumentNullException.ThrowIfNull(note);
        return new CustomerNoteDto(note.Id, note.Body, note.IsPinned, note.AuthorId, note.CreatedAtUtc, note.PinnedAtUtc);
    }
}

/// <summary>Contacts: primary first, then by name. Notes: pinned first, then newest first.</summary>
public sealed record CustomerDto(
    Guid Id, string Number, CustomerNameDto DisplayName, string Type, string? LegalName, string Status,
    string PreferredLanguage, Guid TenantId, Guid BranchId, Guid DepartmentId,
    IReadOnlyList<ContactDto> Contacts, IReadOnlyList<CustomerNoteDto> Notes,
    byte[] RowVersion, string? CreatedBy, DateTimeOffset? CreatedAtUtc, string? ModifiedBy, DateTimeOffset? ModifiedAtUtc)
{
    public static CustomerDto FromDomain(Customer customer)
    {
        ArgumentNullException.ThrowIfNull(customer);

        return new CustomerDto(
            customer.Id.Value, customer.Number.Value, CustomerNameDto.FromDomain(customer.DisplayName),
            customer.Type.ToString(), customer.LegalName, customer.Status.ToString(), customer.PreferredLanguage.Value,
            customer.TenantId.Value, customer.BranchId.Value, customer.DepartmentId.Value,
            [.. customer.Contacts.OrderByDescending(c => c.IsPrimary).ThenBy(c => c.Name, StringComparer.Ordinal).Select(ContactDto.FromDomain)],
            [.. customer.Notes.OrderByDescending(n => n.IsPinned).ThenByDescending(n => n.CreatedAtUtc).Select(CustomerNoteDto.FromDomain)],
            customer.RowVersion, customer.CreatedBy, customer.CreatedAtUtc, customer.ModifiedBy, customer.ModifiedAtUtc);
    }
}

public sealed record CustomerListItemDto(
    Guid Id, string Number, CustomerNameDto DisplayName, string Type, string Status, Guid BranchId, Guid DepartmentId,
    string? PrimaryContactName, string? PrimaryEmail, string? PrimaryPhone, DateTimeOffset? CreatedAtUtc)
{
    public static CustomerListItemDto FromDomain(Customer customer)
    {
        ArgumentNullException.ThrowIfNull(customer);
        Contact? primary = customer.Contacts.FirstOrDefault(c => c.IsPrimary);

        return new CustomerListItemDto(
            customer.Id.Value, customer.Number.Value, CustomerNameDto.FromDomain(customer.DisplayName), customer.Type.ToString(),
            customer.Status.ToString(), customer.BranchId.Value, customer.DepartmentId.Value,
            primary?.Name, primary?.Email?.Value, primary?.Phone?.Value, customer.CreatedAtUtc);
    }
}

public sealed record CustomerLookupDto(Guid Id, string Number, CustomerNameDto DisplayName, string? PrimaryEmail, string? PrimaryPhone)
{
    public static CustomerLookupDto FromDomain(Customer customer)
    {
        ArgumentNullException.ThrowIfNull(customer);
        Contact? primary = customer.Contacts.FirstOrDefault(c => c.IsPrimary);

        return new CustomerLookupDto(
            customer.Id.Value, customer.Number.Value, CustomerNameDto.FromDomain(customer.DisplayName),
            primary?.Email?.Value, primary?.Phone?.Value);
    }
}

public sealed record DuplicateCandidateDto(
    Guid Id, string Number, CustomerNameDto DisplayName, string Status,
    IReadOnlyList<string> MatchedEmails, IReadOnlyList<string> MatchedPhones);

/// <summary>Created = false: nothing was written; the candidates are in-scope customers with a matching contact
/// email or phone. Re-send with ConfirmDuplicate = true to create anyway.</summary>
public sealed record CreateCustomerOutcome(CustomerDto? Customer, IReadOnlyList<DuplicateCandidateDto> DuplicateCandidates)
{
    public bool Created => Customer is not null;

    public static CreateCustomerOutcome Success(CustomerDto customer) => new(customer, []);

    public static CreateCustomerOutcome Duplicates(IReadOnlyList<DuplicateCandidateDto> candidates) => new(null, candidates);
}
