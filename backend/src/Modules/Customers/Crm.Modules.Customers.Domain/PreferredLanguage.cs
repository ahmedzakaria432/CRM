using Crm.SharedKernel;

namespace Crm.Modules.Customers.Domain;

/// <summary>The language a customer prefers to be contacted in: en or ar.</summary>
public sealed class PreferredLanguage : ValueObject
{
    public static readonly PreferredLanguage English = new("en");

    public static readonly PreferredLanguage Arabic = new("ar");

    public string Value { get; }

    private PreferredLanguage(string value)
    {
        Value = value;
    }

    public static PreferredLanguage Create(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return value.Trim().ToLowerInvariant() switch
        {
            "en" => English,
            "ar" => Arabic,
            _ => throw new DomainException(CustomerDomainErrors.LanguageUnsupported),
        };
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
