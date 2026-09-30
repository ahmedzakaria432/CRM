using FluentValidation;

namespace Crm.Modules.Organisation.Application.Commands.CreateBranch;

public sealed class CreateBranchCommandValidator : AbstractValidator<CreateBranchCommand>
{
    public CreateBranchCommandValidator()
    {
        RuleFor(c => c.Code).NotEmpty().Matches("^[A-Za-z0-9-]{2,20}$");
        RuleFor(c => c.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(c => c.NameEn).NotEmpty().MaximumLength(200);
    }
}
