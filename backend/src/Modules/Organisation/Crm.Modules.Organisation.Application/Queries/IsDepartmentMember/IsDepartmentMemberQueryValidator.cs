using FluentValidation;

namespace Crm.Modules.Organisation.Application.Queries.IsDepartmentMember;

public sealed class IsDepartmentMemberQueryValidator : AbstractValidator<IsDepartmentMemberQuery>
{
    public IsDepartmentMemberQueryValidator()
    {
        RuleFor(q => q.UserId).NotEmpty();
        RuleFor(q => q.DepartmentId).NotEmpty();
    }
}
