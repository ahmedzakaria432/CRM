namespace Crm.Modules.Tickets.Domain;

public enum TicketActivityKind
{
    Created,
    Opened,
    AwaitingCustomer,
    PutOnHold,
    Resolved,
    Closed,
    Reopened,
    Cancelled,
    Assigned,
    Claimed,
    Unassigned,
    Transferred,
    PriorityChanged,
    CategoryChanged,
}
