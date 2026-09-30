using Crm.BuildingBlocks.Application.Localization;
using Crm.Modules.Tickets.Application.Localization;
using Crm.Modules.Tickets.Application.Specifications;
using Crm.Modules.Tickets.Domain;
using FluentValidation;

namespace Crm.Modules.Tickets.Application.Validation;

public static class TicketsRules
{
    private static readonly string[] TransitionActions =
    [
        nameof(TicketAction.Open), nameof(TicketAction.AwaitCustomer), nameof(TicketAction.PutOnHold), nameof(TicketAction.Resolve),
        nameof(TicketAction.Close), nameof(TicketAction.Reopen), nameof(TicketAction.Cancel),
    ];

    private static readonly string[] Visibilities = ["public", "internal", "system"];

    /// <summary>The queue is mine, unassigned or all (or absent).</summary>
    public static IRuleBuilderOptions<T, string?> SupportedQueue<T>(this IRuleBuilder<T, string?> rule, ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(localization);

        return rule
            .Must(q => q is null || TicketQueueParser.TryParse(q, out _))
            .WithMessage(_ => localization.Get(TicketsMessageKeys.Validation.QueueUnsupported));
    }

    /// <summary>The sort key is one of createdAt, modifiedAt, priority, number (or absent).</summary>
    public static IRuleBuilderOptions<T, string?> SupportedSort<T>(this IRuleBuilder<T, string?> rule, ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(localization);

        return rule
            .Must(s => s is null || TicketSortParser.TryParse(s, out _))
            .WithMessage(_ => localization.Get(TicketsMessageKeys.Validation.SortUnsupported));
    }

    public static IRuleBuilderOptions<T, string> SupportedStatus<T>(this IRuleBuilder<T, string> rule, ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(localization);

        return rule
            .Must(s => Enum.GetNames<TicketStatus>().Contains(s, StringComparer.OrdinalIgnoreCase))
            .WithMessage(_ => localization.Get(TicketsMessageKeys.Validation.StatusUnsupported));
    }

    public static IRuleBuilderOptions<T, string> SupportedPriority<T>(this IRuleBuilder<T, string> rule, ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(localization);

        return rule
            .Must(p => Enum.GetNames<TicketPriority>().Contains(p, StringComparer.OrdinalIgnoreCase))
            .WithMessage(_ => localization.Get(TicketsMessageKeys.Validation.PriorityUnsupported));
    }

    /// <summary>The priority is Low, Normal, High or Urgent (or absent).</summary>
    public static IRuleBuilderOptions<T, string?> SupportedOptionalPriority<T>(this IRuleBuilder<T, string?> rule, ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(localization);

        return rule
            .Must(p => p is null || Enum.GetNames<TicketPriority>().Contains(p, StringComparer.OrdinalIgnoreCase))
            .WithMessage(_ => localization.Get(TicketsMessageKeys.Validation.PriorityUnsupported));
    }

    /// <summary>A thread visibility: public, internal or system (case-insensitive).</summary>
    public static IRuleBuilderOptions<T, string> SupportedVisibility<T>(this IRuleBuilder<T, string> rule, ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(localization);

        return rule
            .Must(v => Visibilities.Contains(v, StringComparer.OrdinalIgnoreCase))
            .WithMessage(_ => localization.Get(TicketsMessageKeys.Validation.VisibilityUnsupported));
    }

    /// <summary>One of the seven status actions (not Assign, Claim, ...: those have their own endpoints).</summary>
    public static IRuleBuilderOptions<T, string> SupportedTransitionAction<T>(this IRuleBuilder<T, string> rule, ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(localization);

        return rule
            .Must(a => TransitionActions.Contains(a, StringComparer.OrdinalIgnoreCase))
            .WithMessage(_ => localization.Get(TicketsMessageKeys.Validation.ActionUnsupported));
    }
}
