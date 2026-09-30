using Crm.Modules.Customers.Domain;
using FluentValidation;

namespace Crm.Modules.Customers.Application.Queries.FindPortalContactsByEmail;

public sealed class FindPortalContactsByEmailQueryValidator : AbstractValidator<FindPortalContactsByEmailQuery>
{
    public FindPortalContactsByEmailQueryValidator()
    {
        RuleFor(q => q.Email).NotEmpty().MaximumLength(EmailAddress.MaxLength);
    }
}
