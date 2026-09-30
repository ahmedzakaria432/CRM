using FluentValidation;

namespace Crm.Modules.Organisation.Application.Queries.GetDepartmentReference;

public sealed class GetDepartmentReferenceQueryValidator : AbstractValidator<GetDepartmentReferenceQuery>
{
    public GetDepartmentReferenceQueryValidator()
    {
        RuleFor(q => q.DepartmentId).NotEmpty();
    }
}
