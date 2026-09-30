using System.Globalization;
using Crm.BuildingBlocks.Application.Localization;
using Crm.Modules.Tickets.Application.Localization;
using Crm.Modules.Tickets.Domain;
using Crm.SharedKernel.Results;

namespace Crm.Modules.Tickets.Application;

/// <summary>Asks the aggregate's transition table BEFORE the domain call, so an illegal action is a 422 whose
/// detail names the current status in the request's language. The aggregate throws the same code (English) as the
/// backstop if a handler forgets to ask.</summary>
internal static class TicketRefusals
{
    public static Error? NotAllowed(Ticket ticket, TicketAction action, ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        ArgumentNullException.ThrowIfNull(localization);

        return ticket.IsAllowedInCurrentStatus(action)
            ? null
            : TicketsErrors.TransitionNotAllowed(string.Format(
                CultureInfo.CurrentCulture, localization.Get(TicketsMessageKeys.Refusal.TransitionNotAllowed),
                StatusName(ticket.Status, localization)));
    }

    public static Error AlreadyClaimed(string assigneeName, ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(localization);

        return TicketsErrors.AlreadyClaimed(string.Format(
            CultureInfo.CurrentCulture, localization.Get(TicketsMessageKeys.Refusal.AlreadyClaimed), assigneeName));
    }

    private static string StatusName(TicketStatus status, ILocalizationService localization) => localization.Get(status switch
    {
        TicketStatus.New => TicketsMessageKeys.Status.New,
        TicketStatus.Open => TicketsMessageKeys.Status.Open,
        TicketStatus.PendingCustomer => TicketsMessageKeys.Status.PendingCustomer,
        TicketStatus.OnHold => TicketsMessageKeys.Status.OnHold,
        TicketStatus.Resolved => TicketsMessageKeys.Status.Resolved,
        TicketStatus.Closed => TicketsMessageKeys.Status.Closed,
        _ => TicketsMessageKeys.Status.Cancelled,
    });
}
