using Crm.SharedKernel;

namespace Crm.Modules.Customers.Domain;

/// <summary>Bilingual display text (ar + en) for a customer's name. Customers owns its own copy; a missing side is
/// filled with the other, so both languages always render.</summary>
public sealed class LocalizedText : ValueObject
{
    public const int MaxLength = 200;

    public string Arabic { get; }

    public string English { get; }

    private LocalizedText(string arabic, string english)
    {
        Arabic = arabic;
        English = english;
    }

    /// <summary>At least one side is required; a missing side takes the other's text, so both languages always render.</summary>
    public static LocalizedText Create(string? arabic, string? english)
    {
        string ar = arabic?.Trim() ?? string.Empty;
        string en = english?.Trim() ?? string.Empty;

        if (ar.Length == 0 && en.Length == 0)
        {
            throw new DomainException(CustomerDomainErrors.NameRequired);
        }

        if (ar.Length > MaxLength || en.Length > MaxLength)
        {
            throw new DomainException(CustomerDomainErrors.NameTooLong);
        }

        return new LocalizedText(ar.Length == 0 ? en : ar, en.Length == 0 ? ar : en);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Arabic;
        yield return English;
    }
}
