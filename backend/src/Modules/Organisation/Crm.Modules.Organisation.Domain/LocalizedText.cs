using Crm.SharedKernel;

namespace Crm.Modules.Organisation.Domain;

/// <summary>Bilingual display text (ar + en). Lives here, not in the shared kernel - a later module that
/// needs bilingual names defines its own, or promotes this one in a story that says so.</summary>
public sealed class LocalizedText : ValueObject
{
    public string Arabic { get; }

    public string English { get; }

    private LocalizedText(string arabic, string english)
    {
        Arabic = arabic;
        English = english;
    }

    public static LocalizedText Create(string arabic, string english)
    {
        ArgumentNullException.ThrowIfNull(arabic);
        ArgumentNullException.ThrowIfNull(english);

        string trimmedAr = arabic.Trim();
        string trimmedEn = english.Trim();

        if (trimmedAr.Length == 0 || trimmedEn.Length == 0)
        {
            throw new DomainException(new DomainError("Organisation.NameRequired", "Both the Arabic and English names are required."));
        }

        if (trimmedAr.Length > 200 || trimmedEn.Length > 200)
        {
            throw new DomainException(new DomainError("Organisation.NameTooLong", "A name cannot be more than 200 characters."));
        }

        return new LocalizedText(trimmedAr, trimmedEn);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Arabic;
        yield return English;
    }
}
