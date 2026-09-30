using Crm.BuildingBlocks.Application.Localization;
using Crm.Modules.Tickets.Application.Validation;
using FluentValidation;

namespace Crm.Modules.Tickets.Application.Commands.TransitionTicket;

public sealed class TransitionTicketCommandValidator : AbstractValidator<TransitionTicketCommand>
{
    public TransitionTicketCommandValidator(ILocalizationService localization)
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.RowVersion).NotEmpty();
        RuleFor(c => c.Action).NotEmpty().SupportedTransitionAction(localization);
    }
}
