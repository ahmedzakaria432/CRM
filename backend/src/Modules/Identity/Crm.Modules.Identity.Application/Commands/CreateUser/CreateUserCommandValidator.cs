using Crm.BuildingBlocks.Application.Localization;
using Crm.Modules.Identity.Application.Validation;
using FluentValidation;

namespace Crm.Modules.Identity.Application.Commands.CreateUser;

public sealed class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator(ILocalizationService localization)
    {
        RuleFor(c => c.Email).NotEmpty().MaximumLength(254).EmailAddress();
        RuleFor(c => c.DisplayName).NotEmpty().MaximumLength(200);
        RuleFor(c => c.PreferredLocale).SupportedLocale(localization);
        RuleFor(c => c.InitialPassword).StaffPassword(localization);
    }
}
