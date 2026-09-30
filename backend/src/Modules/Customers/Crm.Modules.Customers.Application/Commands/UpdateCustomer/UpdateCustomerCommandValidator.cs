using Crm.BuildingBlocks.Application.Localization;
using Crm.Modules.Customers.Application.Localization;
using Crm.Modules.Customers.Application.Validation;
using Crm.Modules.Customers.Domain;
using FluentValidation;

namespace Crm.Modules.Customers.Application.Commands.UpdateCustomer;

public sealed class UpdateCustomerCommandValidator : AbstractValidator<UpdateCustomerCommand>
{
    public UpdateCustomerCommandValidator(ILocalizationService localization)
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.DisplayNameEn)
            .NotEmpty()
            .WithMessage(_ => localization.Get(CustomersMessageKeys.Validation.NameRequired))
            .When(c => string.IsNullOrWhiteSpace(c.DisplayNameAr));
        RuleFor(c => c.DisplayNameAr).MaximumLength(LocalizedText.MaxLength);
        RuleFor(c => c.DisplayNameEn).MaximumLength(LocalizedText.MaxLength);
        RuleFor(c => c.Type).NotEmpty().IsEnumName(typeof(CustomerType), caseSensitive: false);
        RuleFor(c => c.LegalName).MaximumLength(Customer.LegalNameMaxLength);
        RuleFor(c => c.PreferredLanguage).SupportedLanguage(localization);
        RuleFor(c => c.RowVersion).NotEmpty();
    }
}
