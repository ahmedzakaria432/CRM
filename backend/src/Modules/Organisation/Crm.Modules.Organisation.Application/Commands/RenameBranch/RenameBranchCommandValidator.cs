using FluentValidation;

namespace Crm.Modules.Organisation.Application.Commands.RenameBranch;

public sealed class RenameBranchCommandValidator : AbstractValidator<RenameBranchCommand>
{
    public RenameBranchCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(c => c.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(c => c.RowVersion).NotEmpty();
    }
}
