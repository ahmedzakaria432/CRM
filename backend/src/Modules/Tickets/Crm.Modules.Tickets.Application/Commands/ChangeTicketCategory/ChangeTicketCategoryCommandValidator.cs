using FluentValidation;

namespace Crm.Modules.Tickets.Application.Commands.ChangeTicketCategory;

public sealed class ChangeTicketCategoryCommandValidator : AbstractValidator<ChangeTicketCategoryCommand>
{
    public ChangeTicketCategoryCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.CategoryId).NotEmpty();
        RuleFor(c => c.RowVersion).NotEmpty();
    }
}
