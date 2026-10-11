using Crm.Modules.Organisation.Application.Queries.GetBrandLogo;
using Crm.Modules.Organisation.Tests.Support;

namespace Crm.Modules.Organisation.Tests.Application.Queries.GetBrandLogo;

public sealed class GetBrandLogoQueryValidatorTests
{
    private readonly GetBrandLogoQueryValidator _validator = new();

    [Fact]
    public void AnIdIsValid()
    {
        Expect.Valid(_validator.Validate(new GetBrandLogoQuery(Guid.NewGuid())));
    }

    [Fact]
    public void AnEmptyIdIsInvalid()
    {
        Expect.Invalid(_validator.Validate(new GetBrandLogoQuery(Guid.Empty)), nameof(GetBrandLogoQuery.Id));
    }
}
