using FluentValidation;

namespace Crm.Modules.Identity.Application.Commands.SignIn;

public sealed class SignInCommandValidator : AbstractValidator<SignInCommand>
{
    public SignInCommandValidator()
    {
        RuleFor(c => c.Email).NotEmpty().MaximumLength(254);

        // No format rules on the password: a format rule would turn a typo into a 400 instead of the generic 401.
        RuleFor(c => c.Password).NotEmpty().MaximumLength(128);
    }
}
