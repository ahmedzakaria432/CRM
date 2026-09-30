using FluentValidation;

namespace Crm.Modules.Identity.Application.Commands.RefreshPortalSession;

public sealed class RefreshPortalSessionCommandValidator : AbstractValidator<RefreshPortalSessionCommand>
{
    public RefreshPortalSessionCommandValidator()
    {
        RuleFor(c => c.RefreshToken).NotEmpty().MaximumLength(200);
    }
}
