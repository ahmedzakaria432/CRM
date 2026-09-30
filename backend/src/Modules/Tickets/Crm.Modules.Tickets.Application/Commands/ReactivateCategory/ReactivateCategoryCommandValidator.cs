using FluentValidation;

namespace Crm.Modules.Tickets.Application.Commands.ReactivateCategory;

public sealed class ReactivateCategoryCommandValidator : AbstractValidator<ReactivateCategoryCommand>
{
    public ReactivateCategoryCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.RowVersion).NotEmpty();
    }
}
