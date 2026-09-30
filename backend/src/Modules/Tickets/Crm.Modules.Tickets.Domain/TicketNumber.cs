using System.Globalization;
using Crm.SharedKernel;

namespace Crm.Modules.Tickets.Domain;

/// <summary>"T-" + a sequence value, zero-padded to six digits. The private constructor parameter is named after
/// the mapped property (EF owned-type binding). Built only from the tickets.TicketNumbers sequence.</summary>
public sealed class TicketNumber : ValueObject
{
    public const int MaxLength = 20;
    public const string Prefix = "T-";

    public string Value { get; }

    private TicketNumber(string value)
    {
        Value = value;
    }

    public static TicketNumber FromSequence(long sequenceValue)
    {
        if (sequenceValue <= 0)
        {
            throw new DomainException(TicketDomainErrors.NumberInvalid);
        }

        return new TicketNumber(string.Create(CultureInfo.InvariantCulture, $"{Prefix}{sequenceValue:D6}"));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
