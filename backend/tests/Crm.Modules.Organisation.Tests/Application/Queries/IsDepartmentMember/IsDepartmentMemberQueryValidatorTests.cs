using Crm.Modules.Organisation.Application.Queries.IsDepartmentMember;
using Crm.Modules.Organisation.Tests.Support;

namespace Crm.Modules.Organisation.Tests.Application.Queries.IsDepartmentMember;

public sealed class IsDepartmentMemberQueryValidatorTests
{
    private readonly IsDepartmentMemberQueryValidator _validator = new();

    [Fact]
    public void TwoIdsPass()
    {
        Expect.Valid(_validator.Validate(new IsDepartmentMemberQuery(Ids.User, Ids.DeptA1.Value)));
    }

    [Fact]
    public void AnEmptyUserIdFails()
    {
        Expect.Invalid(_validator.Validate(new IsDepartmentMemberQuery(Guid.Empty, Ids.DeptA1.Value)), nameof(IsDepartmentMemberQuery.UserId));
    }

    [Fact]
    public void AnEmptyDepartmentIdFails()
    {
        Expect.Invalid(_validator.Validate(new IsDepartmentMemberQuery(Ids.User, Guid.Empty)), nameof(IsDepartmentMemberQuery.DepartmentId));
    }
}
