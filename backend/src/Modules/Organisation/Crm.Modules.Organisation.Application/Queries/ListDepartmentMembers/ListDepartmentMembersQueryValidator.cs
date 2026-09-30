using FluentValidation;

namespace Crm.Modules.Organisation.Application.Queries.ListDepartmentMembers;

public sealed class ListDepartmentMembersQueryValidator : AbstractValidator<ListDepartmentMembersQuery>
{
    public ListDepartmentMembersQueryValidator()
    {
        RuleFor(q => q.DepartmentIds).NotNull().Must(ids => ids.Count <= 100);
    }
}
