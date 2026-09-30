namespace Crm.Modules.Tickets.Application.Localization;

/// <summary>Every resource key in <c>Resources/TicketsMessages*.resx</c>. Callers pass these constants to
/// <c>ILocalizationService.Get</c>; a key is never written as a string literal at a call site.</summary>
public static class TicketsMessageKeys
{
    public static class Validation
    {
        public const string QueueUnsupported = "Tickets.Validation.QueueUnsupported";
        public const string SortUnsupported = "Tickets.Validation.SortUnsupported";
        public const string StatusUnsupported = "Tickets.Validation.StatusUnsupported";
        public const string PriorityUnsupported = "Tickets.Validation.PriorityUnsupported";
        public const string ActionUnsupported = "Tickets.Validation.ActionUnsupported";
        public const string VisibilityUnsupported = "Tickets.Validation.VisibilityUnsupported";
        public const string PortalTabUnsupported = "Tickets.Validation.PortalTabUnsupported";
        public const string PortalStatusUnsupported = "Tickets.Validation.PortalStatusUnsupported";
    }

    public static class Refusal
    {
        public const string TransitionNotAllowed = "Tickets.Refusal.TransitionNotAllowed";
        public const string AlreadyClaimed = "Tickets.Refusal.AlreadyClaimed";
    }

    public static class Status
    {
        public const string New = "Tickets.Status.New";
        public const string Open = "Tickets.Status.Open";
        public const string PendingCustomer = "Tickets.Status.PendingCustomer";
        public const string OnHold = "Tickets.Status.OnHold";
        public const string Resolved = "Tickets.Status.Resolved";
        public const string Closed = "Tickets.Status.Closed";
        public const string Cancelled = "Tickets.Status.Cancelled";
    }

    /// <summary>The customer-friendly status names of the portal (Story 47, D9).</summary>
    public static class PortalStatus
    {
        public const string Received = "Tickets.PortalStatus.Received";
        public const string InProgress = "Tickets.PortalStatus.InProgress";
        public const string AwaitingYourReply = "Tickets.PortalStatus.AwaitingYourReply";
        public const string Resolved = "Tickets.PortalStatus.Resolved";
        public const string Closed = "Tickets.PortalStatus.Closed";
    }
}
