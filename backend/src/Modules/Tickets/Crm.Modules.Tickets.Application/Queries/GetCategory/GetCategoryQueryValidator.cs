using FluentValidation;

namespace Crm.Modules.Tickets.Application.Queries.GetCategory;

public sealed class GetCategoryQueryValidator : AbstractValidator<GetCategoryQuery>
{
    public GetCategoryQueryValidator()
    {
        RuleFor(q => q.Id).NotEmpty();
    }
}
