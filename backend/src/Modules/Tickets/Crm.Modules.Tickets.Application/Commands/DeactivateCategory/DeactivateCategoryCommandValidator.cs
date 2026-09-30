using FluentValidation;

namespace Crm.Modules.Tickets.Application.Commands.DeactivateCategory;

public sealed class DeactivateCategoryCommandValidator : AbstractValidator<DeactivateCategoryCommand>
{
    public DeactivateCategoryCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.RowVersion).NotEmpty();
    }
}
