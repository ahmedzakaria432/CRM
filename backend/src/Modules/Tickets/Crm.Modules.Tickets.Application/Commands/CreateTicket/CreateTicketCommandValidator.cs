using Crm.BuildingBlocks.Application.Localization;
using Crm.Modules.Tickets.Application.Validation;
using FluentValidation;

namespace Crm.Modules.Tickets.Application.Commands.CreateTicket;

public sealed class CreateTicketCommandValidator : AbstractValidator<CreateTicketCommand>
{
    public CreateTicketCommandValidator(ILocalizationService localization)
    {
        RuleFor(c => c.CustomerId).NotEmpty();
        RuleFor(c => c.Subject).NotEmpty().MaximumLength(200);
        RuleFor(c => c.Description).NotEmpty().MaximumLength(4000);
        RuleFor(c => c.Priority).SupportedOptionalPriority(localization);
    }
}
