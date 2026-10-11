using Crm.Modules.Organisation.Application.Queries.GetDepartmentReference;
using Crm.Modules.Organisation.Tests.Support;

namespace Crm.Modules.Organisation.Tests.Application.Queries.GetDepartmentReference;

public sealed class GetDepartmentReferenceQueryValidatorTests
{
    private readonly GetDepartmentReferenceQueryValidator _validator = new();

    [Fact]
    public void AnIdIsValid()
    {
        Expect.Valid(_validator.Validate(new GetDepartmentReferenceQuery(Guid.NewGuid())));
    }

    [Fact]
    public void AnEmptyIdIsInvalid()
    {
        Expect.Invalid(_validator.Validate(new GetDepartmentReferenceQuery(Guid.Empty)), nameof(GetDepartmentReferenceQuery.DepartmentId));
    }
}
