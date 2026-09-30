using System.Globalization;
using Crm.SharedKernel;

namespace Crm.Modules.Customers.Domain;

/// <summary>"C-" + a sequence value, zero-padded to six digits. The private constructor parameter is named after
/// the mapped property (EF owned-type binding). Built only from the customers.CustomerNumbers sequence.</summary>
public sealed class CustomerNumber : ValueObject
{
    public const int MaxLength = 20;
    public const string Prefix = "C-";

    public string Value { get; }

    private CustomerNumber(string value)
    {
        Value = value;
    }

    public static CustomerNumber FromSequence(long sequenceValue)
    {
        if (sequenceValue <= 0)
        {
            throw new DomainException(CustomerDomainErrors.NumberInvalid);
        }

        return new CustomerNumber(string.Create(CultureInfo.InvariantCulture, $"{Prefix}{sequenceValue:D6}"));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
