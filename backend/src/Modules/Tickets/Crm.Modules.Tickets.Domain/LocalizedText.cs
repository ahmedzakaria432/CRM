using Crm.SharedKernel;

namespace Crm.Modules.Tickets.Domain;

/// <summary>Bilingual display text (ar + en) for a category name. Tickets owns its own copy; both sides are required.</summary>
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

    public static LocalizedText Create(string arabic, string english)
    {
        ArgumentNullException.ThrowIfNull(arabic);
        ArgumentNullException.ThrowIfNull(english);

        string ar = arabic.Trim();
        string en = english.Trim();

        if (ar.Length == 0 || en.Length == 0)
        {
            throw new DomainException(TicketDomainErrors.CategoryNameRequired);
        }

        if (ar.Length > MaxLength || en.Length > MaxLength)
        {
            throw new DomainException(TicketDomainErrors.CategoryNameTooLong);
        }

        return new LocalizedText(ar, en);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Arabic;
        yield return English;
    }
}
