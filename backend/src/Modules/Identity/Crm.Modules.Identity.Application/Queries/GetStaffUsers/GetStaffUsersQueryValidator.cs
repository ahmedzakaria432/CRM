using FluentValidation;

namespace Crm.Modules.Identity.Application.Queries.GetStaffUsers;

public sealed class GetStaffUsersQueryValidator : AbstractValidator<GetStaffUsersQuery>
{
    public GetStaffUsersQueryValidator()
    {
        RuleFor(q => q.UserIds).NotNull().Must(ids => ids.Count <= 200);
    }
}
