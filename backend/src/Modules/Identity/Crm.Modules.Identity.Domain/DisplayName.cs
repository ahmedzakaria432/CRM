using Crm.SharedKernel;

namespace Crm.Modules.Identity.Domain;

public sealed class DisplayName : ValueObject
{
    public const int MaxLength = 200;

    public string Value { get; }

    private DisplayName(string value)
    {
        Value = value;
    }

    public static DisplayName Create(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        string trimmed = value.Trim();

        if (trimmed.Length == 0)
        {
            throw new DomainException(new DomainError("Identity.DisplayNameRequired", "The display name is required."));
        }

        if (trimmed.Length > MaxLength)
        {
            throw new DomainException(new DomainError("Identity.DisplayNameTooLong", $"The display name is limited to {MaxLength} characters."));
        }

        return new DisplayName(trimmed);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
