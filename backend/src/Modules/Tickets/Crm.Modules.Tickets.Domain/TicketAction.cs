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
    /// <summary>Story 18: ReplyAsAgent / ReceiveCustomerReply. Every status except Closed and Cancelled.</summary>
    Reply,
    /// <summary>Story 18: AddInternalNote. Every status.</summary>
    AddNote,
}
