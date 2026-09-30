using FluentValidation;

namespace Crm.Modules.Customers.Application.Queries.GetPortalContact;

public sealed class GetPortalContactQueryValidator : AbstractValidator<GetPortalContactQuery>
{
    public GetPortalContactQueryValidator()
    {
        RuleFor(q => q.ContactId).NotEmpty();
    }
}
