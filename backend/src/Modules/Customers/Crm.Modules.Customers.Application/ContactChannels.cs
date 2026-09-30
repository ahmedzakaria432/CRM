using Crm.Modules.Customers.Domain;

namespace Crm.Modules.Customers.Application;

internal static class ContactChannels
{
    /// <summary>Blank becomes null; anything else is built into its value object, which throws a 422 DomainException
    /// when malformed (rejected at value-object construction).</summary>
    public static (EmailAddress? Email, PhoneNumber? Phone) Parse(string? email, string? phone) =>
        (string.IsNullOrWhiteSpace(email) ? null : EmailAddress.Create(email),
         string.IsNullOrWhiteSpace(phone) ? null : PhoneNumber.Create(phone));
}
