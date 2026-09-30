using Crm.SharedKernel;

namespace Crm.Modules.Customers.Domain;

public sealed record CustomerId(Guid Value) : StronglyTypedId(Value)
{
    public static CustomerId New() => new(Guid.NewGuid());
}
