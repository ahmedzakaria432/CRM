using Crm.SharedKernel;
using Crm.SharedKernel.Scoping;

namespace Crm.Modules.Customers.Domain;

/// <summary>The aggregate root of the Customers context. Contacts and notes are reachable only through it; every
/// rule that spans contacts (exactly one primary, no duplicate email or phone) is enforced here.</summary>
public sealed class Customer : AuditableEntity<CustomerId>, ISoftDeletable, IHasRowVersion, IOrganisationScoped
{
    public const int LegalNameMaxLength = 300;

    private readonly List<Contact> _contacts = [];
    private readonly List<CustomerNote> _notes = [];

    public CustomerNumber Number { get; private set; }

    public LocalizedText DisplayName { get; private set; }

    public CustomerType Type { get; private set; }

    public string? LegalName { get; private set; }

    public CustomerStatus Status { get; private set; } = CustomerStatus.Active;

    public PreferredLanguage PreferredLanguage { get; private set; }

    public TenantId TenantId { get; private set; } = TenantId.None;

    public BranchId BranchId { get; private set; } = new(Guid.Empty);

    public DepartmentId DepartmentId { get; private set; } = new(Guid.Empty);

    /// <summary>Incremented by every profile, status and contact change (<see cref="MarkChanged"/>). Contacts live in
    /// their own table, so without this a contact-only change would not UPDATE the Customers row - its rowversion
    /// would be neither checked nor bumped, and two concurrent "make primary" calls could both win. Do not remove.</summary>
    public int Revision { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAtUtc { get; private set; }

    public string? DeletedBy { get; private set; }

    public IReadOnlyCollection<Contact> Contacts => _contacts.AsReadOnly();

    public IReadOnlyCollection<CustomerNote> Notes => _notes.AsReadOnly();

    public bool IsActive => Status == CustomerStatus.Active;

    private Customer()
    {
        Number = null!;
        DisplayName = null!;
        PreferredLanguage = null!;
    }

    private Customer(
        CustomerId id, CustomerNumber number, LocalizedText displayName, CustomerType type, string? legalName,
        PreferredLanguage preferredLanguage, TenantId tenantId, BranchId branchId, DepartmentId departmentId) : base(id)
    {
        Number = number;
        DisplayName = displayName;
        Type = type;
        LegalName = legalName;
        PreferredLanguage = preferredLanguage;
        TenantId = tenantId;
        BranchId = branchId;
        DepartmentId = departmentId;
    }

    /// <summary><paramref name="id"/> defaults to a fresh id; the seeder passes a well-known one.</summary>
    public static Customer Create(
        CustomerNumber number, LocalizedText displayName, CustomerType type, string? legalName,
        PreferredLanguage preferredLanguage, TenantId tenantId, BranchId branchId, DepartmentId departmentId,
        CustomerId? id = null)
    {
        ArgumentNullException.ThrowIfNull(number);
        ArgumentNullException.ThrowIfNull(displayName);
        ArgumentNullException.ThrowIfNull(preferredLanguage);
        ArgumentNullException.ThrowIfNull(tenantId);
        ArgumentNullException.ThrowIfNull(branchId);
        ArgumentNullException.ThrowIfNull(departmentId);

        if (tenantId == TenantId.None)
        {
            throw new DomainException(CustomerDomainErrors.ScopeRequired);
        }

        return new Customer(
            id ?? CustomerId.New(), number, displayName, EnsureDefined(type), NormaliseLegalName(type, legalName),
            preferredLanguage, tenantId, branchId, departmentId);
    }

    public void UpdateProfile(LocalizedText displayName, CustomerType type, string? legalName, PreferredLanguage preferredLanguage)
    {
        ArgumentNullException.ThrowIfNull(displayName);
        ArgumentNullException.ThrowIfNull(preferredLanguage);

        string? normalisedLegalName = NormaliseLegalName(EnsureDefined(type), legalName);

        DisplayName = displayName;
        Type = type;
        LegalName = normalisedLegalName;
        PreferredLanguage = preferredLanguage;
        MarkChanged();
    }

    public void Deactivate()
    {
        if (Status == CustomerStatus.Inactive)
        {
            throw new DomainException(CustomerDomainErrors.AlreadyInactive);
        }

        Status = CustomerStatus.Inactive;
        MarkChanged();
    }

    public void Reactivate()
    {
        if (Status == CustomerStatus.Active)
        {
            throw new DomainException(CustomerDomainErrors.AlreadyActive);
        }

        Status = CustomerStatus.Active;
        MarkChanged();
    }

    /// <summary>The first contact is always primary; <paramref name="isPrimary"/> demotes the current primary in the
    /// same operation.</summary>
    public Contact AddContact(
        string name, string? jobTitle, EmailAddress? email, PhoneNumber? phone, bool isPrimary, bool hasPortalAccess,
        Guid? id = null)
    {
        EnsureChannelsUnique(email, phone, exceptContactId: null);

        var contact = new Contact(id ?? Guid.NewGuid(), name, jobTitle, email, phone, hasPortalAccess);
        bool makePrimary = isPrimary || _contacts.Count == 0;

        _contacts.Add(contact);

        if (makePrimary)
        {
            PromoteToPrimary(contact);
        }

        MarkChanged();
        return contact;
    }

    public void UpdateContact(Guid contactId, string name, string? jobTitle, EmailAddress? email, PhoneNumber? phone, bool hasPortalAccess)
    {
        Contact contact = FindContact(contactId);
        EnsureChannelsUnique(email, phone, exceptContactId: contactId);

        contact.Change(name, jobTitle, email, phone, hasPortalAccess);
        MarkChanged();
    }

    public void SetPrimaryContact(Guid contactId)
    {
        PromoteToPrimary(FindContact(contactId));
        MarkChanged();
    }

    public void RemoveContact(Guid contactId)
    {
        Contact contact = FindContact(contactId);

        if (contact.IsPrimary && _contacts.Count > 1)
        {
            throw new DomainException(CustomerDomainErrors.PrimaryContactRemoval);
        }

        _contacts.Remove(contact);
        MarkChanged();
    }

    public bool HasContact(Guid contactId) => _contacts.Exists(c => c.Id == contactId);

    public CustomerNote AddNote(string body, Guid authorId, DateTimeOffset createdAtUtc, Guid? id = null)
    {
        var note = new CustomerNote(id ?? Guid.NewGuid(), body, authorId, createdAtUtc);
        _notes.Add(note);
        return note;
    }

    public void PinNote(Guid noteId, DateTimeOffset pinnedAtUtc) => FindNote(noteId).Pin(pinnedAtUtc);

    public void UnpinNote(Guid noteId) => FindNote(noteId).Unpin();

    public void RemoveNote(Guid noteId) => _notes.Remove(FindNote(noteId));

    public bool HasNote(Guid noteId) => _notes.Exists(n => n.Id == noteId);

    private void PromoteToPrimary(Contact contact)
    {
        foreach (Contact other in _contacts)
        {
            other.ClearPrimary();
        }

        contact.MarkPrimary();
    }

    private void EnsureChannelsUnique(EmailAddress? email, PhoneNumber? phone, Guid? exceptContactId)
    {
        if (email is not null && _contacts.Exists(c => c.Id != exceptContactId && email.Equals(c.Email)))
        {
            throw new DomainException(CustomerDomainErrors.DuplicateContactEmail);
        }

        if (phone is not null && _contacts.Exists(c => c.Id != exceptContactId && phone.Equals(c.Phone)))
        {
            throw new DomainException(CustomerDomainErrors.DuplicateContactPhone);
        }
    }

    private Contact FindContact(Guid contactId) =>
        _contacts.Find(c => c.Id == contactId) ?? throw new DomainException(CustomerDomainErrors.ContactNotFound);

    private CustomerNote FindNote(Guid noteId) =>
        _notes.Find(n => n.Id == noteId) ?? throw new DomainException(CustomerDomainErrors.NoteNotFound);

    private void MarkChanged() => Revision++;

    private static CustomerType EnsureDefined(CustomerType type) =>
        Enum.IsDefined(type) ? type : throw new DomainException(CustomerDomainErrors.TypeUnsupported);

    private static string? NormaliseLegalName(CustomerType type, string? legalName)
    {
        string? trimmed = string.IsNullOrWhiteSpace(legalName) ? null : legalName.Trim();

        if (type == CustomerType.Organisation && trimmed is null)
        {
            throw new DomainException(CustomerDomainErrors.LegalNameRequired);
        }

        if (type == CustomerType.Individual && trimmed is not null)
        {
            throw new DomainException(CustomerDomainErrors.LegalNameNotAllowed);
        }

        if (trimmed is { Length: > LegalNameMaxLength })
        {
            throw new DomainException(CustomerDomainErrors.LegalNameTooLong);
        }

        return trimmed;
    }
}
