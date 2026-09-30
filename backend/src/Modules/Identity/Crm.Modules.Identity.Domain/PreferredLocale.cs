using Crm.SharedKernel;

namespace Crm.Modules.Identity.Domain;

/// <summary>The language a user prefers: en or ar. Applied by the frontend at sign-in.</summary>
public sealed class PreferredLocale : ValueObject
{
    public static readonly PreferredLocale English = new("en");

    public static readonly PreferredLocale Arabic = new("ar");

    public string Value { get; }

    private PreferredLocale(string value)
    {
        Value = value;
    }

    public static PreferredLocale Create(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return value.Trim().ToLowerInvariant() switch
        {
            "en" => English,
            "ar" => Arabic,
            _ => throw new DomainException(new DomainError("Identity.LocaleUnsupported", "The preferred language must be en or ar.")),
        };
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
