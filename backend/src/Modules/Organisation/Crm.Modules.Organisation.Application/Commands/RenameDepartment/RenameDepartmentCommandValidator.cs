using FluentValidation;

namespace Crm.Modules.Organisation.Application.Commands.RenameDepartment;

public sealed class RenameDepartmentCommandValidator : AbstractValidator<RenameDepartmentCommand>
{
    public RenameDepartmentCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(c => c.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(c => c.RowVersion).NotEmpty();
    }
}
