using Crm.SharedKernel;

namespace Crm.Modules.Customers.Domain;

/// <summary>Every 422 the Customers domain raises. The codes are the frontend's errors.code.* contract - never rename one.</summary>
internal static class CustomerDomainErrors
{
    public static readonly DomainError ScopeRequired = new("Customers.ScopeRequired", "A customer must belong to a tenant, branch, and department.");
    public static readonly DomainError NumberInvalid = new("Customers.NumberInvalid", "The customer number sequence value is not valid.");
    public static readonly DomainError TypeUnsupported = new("Customers.TypeUnsupported", "The customer type must be Individual or Organisation.");
    public static readonly DomainError NameRequired = new("Customers.NameRequired", "Enter the customer's name in Arabic or English.");
    public static readonly DomainError NameTooLong = new("Customers.NameTooLong", "A customer name cannot be more than 200 characters.");
    public static readonly DomainError LegalNameRequired = new("Customers.LegalNameRequired", "An organisation customer requires a legal name.");
    public static readonly DomainError LegalNameNotAllowed = new("Customers.LegalNameNotAllowed", "An individual customer cannot have a legal name.");
    public static readonly DomainError LegalNameTooLong = new("Customers.LegalNameTooLong", "A legal name cannot be more than 300 characters.");
    public static readonly DomainError LanguageUnsupported = new("Customers.LanguageUnsupported", "The preferred language must be en or ar.");
    public static readonly DomainError AlreadyActive = new("Customers.AlreadyActive", "The customer is already active.");
    public static readonly DomainError AlreadyInactive = new("Customers.AlreadyInactive", "The customer is already inactive.");
    public static readonly DomainError EmailInvalid = new("Customers.EmailInvalid", "The email address is not valid.");
    public static readonly DomainError PhoneInvalid = new("Customers.PhoneInvalid", "The phone number must be in international format, for example +966501234567.");
    public static readonly DomainError ContactNameRequired = new("Customers.ContactNameRequired", "A contact needs a name.");
    public static readonly DomainError ContactNameTooLong = new("Customers.ContactNameTooLong", "A contact name cannot be more than 200 characters.");
    public static readonly DomainError JobTitleTooLong = new("Customers.JobTitleTooLong", "A job title cannot be more than 100 characters.");
    public static readonly DomainError ContactChannelRequired = new("Customers.ContactChannelRequired", "A contact needs an email or a phone number.");
    public static readonly DomainError PortalAccessRequiresEmail = new("Customers.PortalAccessRequiresEmail", "Portal access requires the contact to have an email address.");
    public static readonly DomainError DuplicateContactEmail = new("Customers.DuplicateContactEmail", "Another contact of this customer already has this email address.");
    public static readonly DomainError DuplicateContactPhone = new("Customers.DuplicateContactPhone", "Another contact of this customer already has this phone number.");
    public static readonly DomainError PrimaryContactRemoval = new("Customers.PrimaryContactRemoval", "Make another contact primary before removing this one.");
    public static readonly DomainError ContactNotFound = new("Customers.ContactNotFound", "The contact does not belong to this customer.");
    public static readonly DomainError NoteNotFound = new("Customers.NoteNotFound", "The note does not belong to this customer.");
    public static readonly DomainError NoteBodyRequired = new("Customers.NoteBodyRequired", "A note cannot be empty.");
    public static readonly DomainError NoteBodyTooLong = new("Customers.NoteBodyTooLong", "A note cannot be more than 4000 characters.");
    public static readonly DomainError NoteAuthorRequired = new("Customers.NoteAuthorRequired", "A note needs an author.");
}
