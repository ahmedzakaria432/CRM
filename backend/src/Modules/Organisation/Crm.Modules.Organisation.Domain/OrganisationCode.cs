using System.Text.RegularExpressions;
using Crm.SharedKernel;

namespace Crm.Modules.Organisation.Domain;

public sealed partial class OrganisationCode : ValueObject
{
    public string Value { get; }

    private OrganisationCode(string value)
    {
        Value = value;
    }

    public static OrganisationCode Create(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        string trimmed = value.Trim().ToUpperInvariant();

        if (!CodePattern().IsMatch(trimmed))
        {
            throw new DomainException(new DomainError("Organisation.CodeInvalid", "Code must be 2-20 characters of A-Z, 0-9, or '-'."));
        }

        return new OrganisationCode(trimmed);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    [GeneratedRegex("^[A-Z0-9-]{2,20}$")]
    private static partial Regex CodePattern();
}
