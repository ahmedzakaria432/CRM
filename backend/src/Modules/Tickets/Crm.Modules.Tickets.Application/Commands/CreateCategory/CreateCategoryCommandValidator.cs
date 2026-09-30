using FluentValidation;

namespace Crm.Modules.Tickets.Application.Commands.CreateCategory;

public sealed class CreateCategoryCommandValidator : AbstractValidator<CreateCategoryCommand>
{
    public CreateCategoryCommandValidator()
    {
        RuleFor(c => c.DepartmentId).NotEmpty();
        RuleFor(c => c.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(c => c.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(c => c.DisplayOrder).InclusiveBetween(0, 9999);
    }
}
