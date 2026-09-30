using FluentValidation;

namespace Crm.Modules.Identity.Application.Commands.RefreshSession;

public sealed class RefreshSessionCommandValidator : AbstractValidator<RefreshSessionCommand>
{
    public RefreshSessionCommandValidator()
    {
        RuleFor(c => c.RefreshToken).NotEmpty().MaximumLength(200);
    }
}
