using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Crm.SharedKernel;

namespace Crm.Modules.Customers.Domain;

/// <summary>A customer contact email: trimmed, lower-case invariant, at most 254 characters, local@domain.tld. The
/// private constructor parameter is named after the mapped property, which EF's owned-type binding requires.</summary>
public sealed partial class EmailAddress : ValueObject
{
    public const int MaxLength = 254;

    public string Value { get; }

    private EmailAddress(string value)
    {
        Value = value;
    }

    public static EmailAddress Create(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (!TryCreate(value, out EmailAddress? email))
        {
            throw new DomainException(CustomerDomainErrors.EmailInvalid);
        }

        return email;
    }

    /// <summary>Never throws; lookups use it so a malformed address means no match, not a 422.</summary>
    public static bool TryCreate(string? value, [NotNullWhen(true)] out EmailAddress? email)
    {
        email = null;

        if (value is null)
        {
            return false;
        }

        string normalised = value.Trim().ToLowerInvariant();

        if (normalised.Length is 0 or > MaxLength || !Shape().IsMatch(normalised))
        {
            return false;
        }

        email = new EmailAddress(normalised);
        return true;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex Shape();
}
