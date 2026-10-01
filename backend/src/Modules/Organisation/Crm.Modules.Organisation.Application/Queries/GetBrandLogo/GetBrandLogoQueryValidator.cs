using FluentValidation;

namespace Crm.Modules.Organisation.Application.Queries.GetBrandLogo;

public sealed class GetBrandLogoQueryValidator : AbstractValidator<GetBrandLogoQuery>
{
    public GetBrandLogoQueryValidator()
    {
        RuleFor(q => q.Id).NotEmpty();
    }
}
