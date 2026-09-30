using Crm.SharedKernel;

namespace Crm.Modules.Customers.Domain;

/// <summary>Child entity - no DbSet, no repository. Reachable only through Customer, which owns every rule that
/// spans contacts (one primary, no duplicate email/phone). The rules of a single contact live here.</summary>
public sealed class Contact : Entity<Guid>
{
    public const int NameMaxLength = 200;
    public const int JobTitleMaxLength = 100;

    public string Name { get; private set; }

    public string? JobTitle { get; private set; }

    public EmailAddress? Email { get; private set; }

    public PhoneNumber? Phone { get; private set; }

    public bool IsPrimary { get; private set; }

    public bool HasPortalAccess { get; private set; }

    private Contact()
    {
        Name = string.Empty;
    }

    internal Contact(Guid id, string name, string? jobTitle, EmailAddress? email, PhoneNumber? phone, bool hasPortalAccess)
        : base(id)
    {
        Name = string.Empty;
        Change(name, jobTitle, email, phone, hasPortalAccess);
    }

    internal void Change(string name, string? jobTitle, EmailAddress? email, PhoneNumber? phone, bool hasPortalAccess)
    {
        ArgumentNullException.ThrowIfNull(name);

        string trimmedName = name.Trim();
        if (trimmedName.Length == 0)
        {
            throw new DomainException(CustomerDomainErrors.ContactNameRequired);
        }

        if (trimmedName.Length > NameMaxLength)
        {
            throw new DomainException(CustomerDomainErrors.ContactNameTooLong);
        }

        string? trimmedTitle = string.IsNullOrWhiteSpace(jobTitle) ? null : jobTitle.Trim();
        if (trimmedTitle is { Length: > JobTitleMaxLength })
        {
            throw new DomainException(CustomerDomainErrors.JobTitleTooLong);
        }

        if (email is null && phone is null)
        {
            throw new DomainException(CustomerDomainErrors.ContactChannelRequired);
        }

        if (hasPortalAccess && email is null)
        {
            throw new DomainException(CustomerDomainErrors.PortalAccessRequiresEmail);
        }

        Name = trimmedName;
        JobTitle = trimmedTitle;
        Email = email;
        Phone = phone;
        HasPortalAccess = hasPortalAccess;
    }

    internal void MarkPrimary() => IsPrimary = true;

    internal void ClearPrimary() => IsPrimary = false;
}
