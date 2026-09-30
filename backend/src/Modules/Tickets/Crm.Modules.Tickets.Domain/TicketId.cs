using Crm.SharedKernel;

namespace Crm.Modules.Tickets.Domain;

public sealed record TicketId(Guid Value) : StronglyTypedId(Value)
{
    public static TicketId New() => new(Guid.NewGuid());
}
