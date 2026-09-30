using Crm.BuildingBlocks.Application.Localization;
using Crm.Modules.Identity.Application.Validation;
using FluentValidation;

namespace Crm.Modules.Identity.Application.Commands.ProvisionPortalAccount;

public sealed class ProvisionPortalAccountCommandValidator : AbstractValidator<ProvisionPortalAccountCommand>
{
    public ProvisionPortalAccountCommandValidator(ILocalizationService localization)
    {
        RuleFor(c => c.CustomerId).NotEmpty();
        RuleFor(c => c.ContactId).NotEmpty();
        RuleFor(c => c.Password).StaffPassword(localization);
    }
}
