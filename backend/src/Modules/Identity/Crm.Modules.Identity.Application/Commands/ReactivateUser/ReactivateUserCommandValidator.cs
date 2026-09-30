using FluentValidation;

namespace Crm.Modules.Identity.Application.Commands.ReactivateUser;

public sealed class ReactivateUserCommandValidator : AbstractValidator<ReactivateUserCommand>
{
    public ReactivateUserCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.RowVersion).NotEmpty();
    }
}
