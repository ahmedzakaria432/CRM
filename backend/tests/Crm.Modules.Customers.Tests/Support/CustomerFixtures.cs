using Crm.Modules.Customers.Domain;
using Crm.SharedKernel.Scoping;

namespace Crm.Modules.Customers.Tests.Support;

/// <summary>Builders over the public domain API only. A class named Customers would collide with the
/// Crm.Modules.Customers namespace, hence the name.</summary>
public static class CustomerFixtures
{
    public static EmailAddress Email(string value) => EmailAddress.Create(value);

    public static PhoneNumber Phone(string value) => PhoneNumber.Create(value);

    public static CustomerNumber Number(long sequence = 1) => CustomerNumber.FromSequence(sequence);

    public static LocalizedText Name(string? ar = "أحمد", string? en = "Ahmed") => LocalizedText.Create(ar, en);

    public static Customer Individual(
        long sequence = 1, string? ar = "أحمد", string? en = "Ahmed", string language = "en", TenantId? tenant = null,
        BranchId? branch = null, DepartmentId? department = null, CustomerId? id = null) =>
        Customer.Create(
            Number(sequence), Name(ar, en), CustomerType.Individual, null, PreferredLanguage.Create(language),
            tenant ?? Ids.TenantA, branch ?? Ids.BranchA1, department ?? Ids.DepartmentA1, id);

    public static Customer Organisation(
        long sequence = 1, string? ar = "نجد", string? en = "Najd", string? legalName = "Najd Co", string language = "en",
        TenantId? tenant = null, BranchId? branch = null, DepartmentId? department = null, CustomerId? id = null) =>
        Customer.Create(
            Number(sequence), Name(ar, en), CustomerType.Organisation, legalName, PreferredLanguage.Create(language),
            tenant ?? Ids.TenantA, branch ?? Ids.BranchA1, department ?? Ids.DepartmentA1, id);

    /// <summary>Adds a contact with explicit channels. The first contact becomes primary on its own.</summary>
    public static Contact AddSimpleContact(
        this Customer customer, string name, string? email, string? phone = null, bool primary = false, bool portal = false, Guid? id = null)
    {
        ArgumentNullException.ThrowIfNull(customer);

        return customer.AddContact(
            name, null, email is null ? null : EmailAddress.Create(email), phone is null ? null : PhoneNumber.Create(phone),
            primary, portal, id);
    }

    /// <summary>Contacts "Contact 1" ... with c1@example.com / +966500000001 and so on; the first is primary.</summary>
    public static IReadOnlyList<Contact> WithContacts(this Customer customer, int count)
    {
        ArgumentNullException.ThrowIfNull(customer);

        List<Contact> created = [];
        for (int n = 1; n <= count; n++)
        {
            created.Add(customer.AddSimpleContact($"Contact {n}", $"c{n}@example.com", $"+96650000000{n}"));
        }

        return created;
    }
}
