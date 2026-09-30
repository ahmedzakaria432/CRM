namespace Crm.Modules.Tickets.Application.Specifications;

public enum TicketQueueKind
{
    All,
    Mine,
    Unassigned,
}

public static class TicketQueueParser
{
    public static bool TryParse(string value, out TicketQueueKind queue)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (value.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            queue = TicketQueueKind.All;
        }
        else if (value.Equals("mine", StringComparison.OrdinalIgnoreCase))
        {
            queue = TicketQueueKind.Mine;
        }
        else if (value.Equals("unassigned", StringComparison.OrdinalIgnoreCase))
        {
            queue = TicketQueueKind.Unassigned;
        }
        else
        {
            queue = TicketQueueKind.All;
            return false;
        }

        return true;
    }
}
