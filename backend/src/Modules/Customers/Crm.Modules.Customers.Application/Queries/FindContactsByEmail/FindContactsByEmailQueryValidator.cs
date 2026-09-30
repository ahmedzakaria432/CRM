using Crm.Modules.Customers.Domain;
using FluentValidation;

namespace Crm.Modules.Customers.Application.Queries.FindContactsByEmail;

public sealed class FindContactsByEmailQueryValidator : AbstractValidator<FindContactsByEmailQuery>
{
    public FindContactsByEmailQueryValidator()
    {
        RuleFor(q => q.Email).NotEmpty().MaximumLength(EmailAddress.MaxLength);
    }
}
