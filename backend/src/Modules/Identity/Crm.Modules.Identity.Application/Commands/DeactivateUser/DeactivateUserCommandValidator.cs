using FluentValidation;

namespace Crm.Modules.Identity.Application.Commands.DeactivateUser;

public sealed class DeactivateUserCommandValidator : AbstractValidator<DeactivateUserCommand>
{
    public DeactivateUserCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.RowVersion).NotEmpty();
    }
}
