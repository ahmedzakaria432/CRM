using Crm.BuildingBlocks.Application.Localization;
using Crm.Modules.Tickets.Application.Validation;
using FluentValidation;

namespace Crm.Modules.Tickets.Application.Commands.ChangeTicketPriority;

public sealed class ChangeTicketPriorityCommandValidator : AbstractValidator<ChangeTicketPriorityCommand>
{
    public ChangeTicketPriorityCommandValidator(ILocalizationService localization)
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.RowVersion).NotEmpty();
        RuleFor(c => c.Priority).NotEmpty().SupportedPriority(localization);
    }
}
