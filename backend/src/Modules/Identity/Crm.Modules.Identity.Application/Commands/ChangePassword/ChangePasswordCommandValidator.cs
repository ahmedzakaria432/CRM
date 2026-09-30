using Crm.BuildingBlocks.Application.Localization;
using Crm.Modules.Identity.Application.Validation;
using FluentValidation;

namespace Crm.Modules.Identity.Application.Commands.ChangePassword;

public sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator(ILocalizationService localization)
    {
        RuleFor(c => c.CurrentPassword).NotEmpty();
        RuleFor(c => c.NewPassword).StaffPassword(localization).NotEqual(c => c.CurrentPassword);
    }
}
