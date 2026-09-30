using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Crm.SharedKernel;

namespace Crm.Modules.Customers.Domain;

/// <summary>E.164: '+', a non-zero first digit, at most 15 digits. Spaces, '-', '(', ')' and '.' are stripped first,
/// so "+966 50 123 4567" is stored "+966501234567"; "0501234567" (no country code) is rejected.</summary>
public sealed partial class PhoneNumber : ValueObject
{
    public const int MaxLength = 16;

    public string Value { get; }

    private PhoneNumber(string value)
    {
        Value = value;
    }

    public static PhoneNumber Create(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (!TryCreate(value, out PhoneNumber? phone))
        {
            throw new DomainException(CustomerDomainErrors.PhoneInvalid);
        }

        return phone;
    }

    public static bool TryCreate(string? value, [NotNullWhen(true)] out PhoneNumber? phone)
    {
        phone = null;

        if (value is null)
        {
            return false;
        }

        string normalised = Separators().Replace(value.Trim(), string.Empty);

        if (!E164().IsMatch(normalised))
        {
            return false;
        }

        phone = new PhoneNumber(normalised);
        return true;
    }

    /// <summary>The search-side normalisation: separators stripped, no shape check. Empty when nothing is left.</summary>
    public static string StripSeparators(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Separators().Replace(value.Trim(), string.Empty);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    [GeneratedRegex(@"[\s\-().]")]
    private static partial Regex Separators();

    [GeneratedRegex(@"^\+[1-9]\d{1,14}$")]
    private static partial Regex E164();
}
