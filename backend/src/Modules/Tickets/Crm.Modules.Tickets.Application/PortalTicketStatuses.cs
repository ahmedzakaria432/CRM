using Crm.Modules.Tickets.Domain;

namespace Crm.Modules.Tickets.Application;

/// <summary>The customer-friendly statuses and tabs of the portal (Story 47, D9).</summary>
internal static class PortalTicketStatuses
{
    public const string Received = "Received";
    public const string InProgress = "InProgress";
    public const string AwaitingYourReply = "AwaitingYourReply";
    public const string Resolved = "Resolved";
    public const string Closed = "Closed";

    public const string OpenTabName = "open";
    public const string ClosedTabName = "closed";

    public static readonly IReadOnlyList<string> All = [Received, InProgress, AwaitingYourReply, Resolved, Closed];

    public static readonly IReadOnlyList<string> Tabs = [OpenTabName, ClosedTabName];

    public static readonly IReadOnlyList<TicketStatus> OpenTab =
        [TicketStatus.New, TicketStatus.Open, TicketStatus.PendingCustomer, TicketStatus.OnHold];

    public static readonly IReadOnlyList<TicketStatus> ClosedTab =
        [TicketStatus.Resolved, TicketStatus.Closed, TicketStatus.Cancelled];

    public static string Name(TicketStatus status) => status switch
    {
        TicketStatus.New => Received,
        TicketStatus.Open or TicketStatus.OnHold => InProgress,
        TicketStatus.PendingCustomer => AwaitingYourReply,
        TicketStatus.Resolved => Resolved,
        _ => Closed,
    };

    /// <summary>The domain statuses behind one portal status name (empty for an unknown name).</summary>
    public static IReadOnlyList<TicketStatus> Statuses(string portalName) =>
        [.. Enum.GetValues<TicketStatus>().Where(s => string.Equals(Name(s), portalName, StringComparison.OrdinalIgnoreCase))];

    public static IReadOnlyList<TicketStatus> TabStatuses(string? tab) =>
        string.Equals(tab, ClosedTabName, StringComparison.OrdinalIgnoreCase) ? ClosedTab : OpenTab;
}
