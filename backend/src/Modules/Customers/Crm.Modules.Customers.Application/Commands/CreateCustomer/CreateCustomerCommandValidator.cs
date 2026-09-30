using Crm.BuildingBlocks.Application.Localization;
using Crm.Modules.Customers.Application.Localization;
using Crm.Modules.Customers.Application.Validation;
using Crm.Modules.Customers.Domain;
using FluentValidation;

namespace Crm.Modules.Customers.Application.Commands.CreateCustomer;

// Deliberately no "legal name required for organisations" and no email/phone format rule: those are aggregate and
// value-object rules and surface as 422 domain-rule, not 400.
public sealed class CreateCustomerCommandValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerCommandValidator(ILocalizationService localization)
    {
        RuleFor(c => c.DisplayNameEn)
            .NotEmpty()
            .WithMessage(_ => localization.Get(CustomersMessageKeys.Validation.NameRequired))
            .When(c => string.IsNullOrWhiteSpace(c.DisplayNameAr));
        RuleFor(c => c.DisplayNameAr).MaximumLength(LocalizedText.MaxLength);
        RuleFor(c => c.DisplayNameEn).MaximumLength(LocalizedText.MaxLength);
        RuleFor(c => c.Type).NotEmpty().IsEnumName(typeof(CustomerType), caseSensitive: false);
        RuleFor(c => c.LegalName).MaximumLength(Customer.LegalNameMaxLength);
        RuleFor(c => c.PreferredLanguage).SupportedLanguage(localization);
        RuleFor(c => c.Contacts).NotNull().Must(c => c.Count <= 20);
        RuleForEach(c => c.Contacts).ChildRules(contact =>
        {
            contact.RuleFor(x => x.Name).NotEmpty().MaximumLength(Contact.NameMaxLength);
            contact.RuleFor(x => x.JobTitle).MaximumLength(Contact.JobTitleMaxLength);
            contact.RuleFor(x => x.Email).MaximumLength(EmailAddress.MaxLength);
            contact.RuleFor(x => x.Phone).MaximumLength(32);
        });
    }
}
