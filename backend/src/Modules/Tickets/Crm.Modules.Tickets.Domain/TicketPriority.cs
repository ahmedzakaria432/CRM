namespace Crm.Modules.Tickets.Domain;

/// <summary>Stored as an int (1-4) so it sorts by rank.</summary>
public enum TicketPriority
{
    Low = 1,
    Normal = 2,
    High = 3,
    Urgent = 4,
}
