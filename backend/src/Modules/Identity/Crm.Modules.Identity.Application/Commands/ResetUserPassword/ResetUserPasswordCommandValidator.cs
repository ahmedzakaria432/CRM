using Crm.BuildingBlocks.Application.Localization;
using Crm.Modules.Identity.Application.Validation;
using FluentValidation;

namespace Crm.Modules.Identity.Application.Commands.ResetUserPassword;

public sealed class ResetUserPasswordCommandValidator : AbstractValidator<ResetUserPasswordCommand>
{
    public ResetUserPasswordCommandValidator(ILocalizationService localization)
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.NewPassword).StaffPassword(localization);
        RuleFor(c => c.RowVersion).NotEmpty();
    }
}
