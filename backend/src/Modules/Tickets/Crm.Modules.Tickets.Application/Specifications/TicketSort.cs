namespace Crm.Modules.Tickets.Application.Specifications;

public enum TicketSort
{
    CreatedAt,
    ModifiedAt,
    Priority,
    Number,
}

public static class TicketSortParser
{
    public static bool TryParse(string value, out TicketSort sort)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (value.Equals("createdAt", StringComparison.OrdinalIgnoreCase))
        {
            sort = TicketSort.CreatedAt;
        }
        else if (value.Equals("modifiedAt", StringComparison.OrdinalIgnoreCase))
        {
            sort = TicketSort.ModifiedAt;
        }
        else if (value.Equals("priority", StringComparison.OrdinalIgnoreCase))
        {
            sort = TicketSort.Priority;
        }
        else if (value.Equals("number", StringComparison.OrdinalIgnoreCase))
        {
            sort = TicketSort.Number;
        }
        else
        {
            sort = TicketSort.CreatedAt;
            return false;
        }

        return true;
    }
}
