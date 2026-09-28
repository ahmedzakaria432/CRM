// CANONICAL SLICE - copy this shape for every new use case (CLAUDE.md, "Backend", CQRS).
// DELETE this whole Sample module in the story that lands the first real use case (Story 11).

using Crm.SharedKernel;

namespace Crm.Modules.Sample.Domain;

/// <summary>
/// The owned-type mapping in SampleAggregateConfiguration binds this constructor by parameter
/// name - EF Core 10 requires the parameter name to match the mapped property name
/// (case-insensitive), or model build fails with "No suitable constructor was found". Every later
/// value object (EmailAddress, PhoneNumber, Money, ...) inherits this same constraint.
/// </summary>
public sealed class SampleName : ValueObject
{
    public string Value { get; }

    private SampleName(string value)
    {
        Value = value;
    }

    public static SampleName Create(string value)
    {
        string trimmed = value.Trim();

        if (trimmed.Length == 0)
        {
            throw new DomainException(new DomainError("Sample.NameRequired", "Sample name cannot be empty."));
        }

        return new SampleName(trimmed);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
