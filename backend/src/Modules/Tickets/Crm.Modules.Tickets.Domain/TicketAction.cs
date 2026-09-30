namespace Crm.Modules.Tickets.Domain;

public enum TicketAction
{
    Open,
    AwaitCustomer,
    PutOnHold,
    Resolve,
    Close,
    Reopen,
    Cancel,
    Assign,
    Claim,
    Unassign,
    Transfer,
    ChangePriority,
    ChangeCategory,
}
