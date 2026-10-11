using Crm.Modules.Tickets.Domain;

namespace Crm.Modules.Tickets.Tests.Domain;

/// <summary>The enum names and order are the storage and API contract: TicketConfiguration stores them and the DTOs return names.</summary>
public sealed class EnumContractTests
{
    [Fact]
    public void TicketStatusNamesAndOrder()
    {
        Assert.Equal(
            ["New", "Open", "PendingCustomer", "OnHold", "Resolved", "Closed", "Cancelled"],
            Enum.GetNames<TicketStatus>());
    }

    [Fact]
    public void TicketChannelNamesAndOrder()
    {
        Assert.Equal(["Agent", "Portal"], Enum.GetNames<TicketChannel>());
    }

    [Fact]
    public void TicketActivityKindNamesAndOrder()
    {
        Assert.Equal(
            [
                "Created", "Opened", "AwaitingCustomer", "PutOnHold", "Resolved", "Closed", "Reopened", "Cancelled", "Assigned",
                "Claimed", "Unassigned", "Transferred", "PriorityChanged", "CategoryChanged",
            ],
            Enum.GetNames<TicketActivityKind>());
    }

    [Fact]
    public void TicketMessageDirectionNamesAndOrder()
    {
        Assert.Equal(["Inbound", "Outbound"], Enum.GetNames<TicketMessageDirection>());
    }

    [Fact]
    public void TicketActionNamesAndOrder()
    {
        Assert.Equal(
            [
                "Open", "AwaitCustomer", "PutOnHold", "Resolve", "Close", "Reopen", "Cancel", "Assign", "Claim", "Unassign",
                "Transfer", "ChangePriority", "ChangeCategory", "Reply", "AddNote",
            ],
            Enum.GetNames<TicketAction>());
    }

    [Fact]
    public void CustomerTicketActionNamesAndOrder()
    {
        Assert.Equal(["Reply", "RequestClosure", "Reopen"], Enum.GetNames<CustomerTicketAction>());
    }

    [Fact]
    public void TicketPriorityIsStoredAsOneToFourSoItSortsByRank()
    {
        Assert.Equal(["Low", "Normal", "High", "Urgent"], Enum.GetNames<TicketPriority>());
        Assert.Equal([1, 2, 3, 4], Enum.GetValues<TicketPriority>().Select(p => (int)p));
    }
}
