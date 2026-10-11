using Crm.Modules.Organisation.Application.Commands.RemoveMembership;
using Crm.Modules.Organisation.Tests.Support;

namespace Crm.Modules.Organisation.Tests.Application.Commands.RemoveMembership;

public sealed class RemoveMembershipCommandValidatorTests
{
    private readonly RemoveMembershipCommandValidator _validator = new();

    [Fact]
    public void AnIdIsValid()
    {
        Expect.Valid(_validator.Validate(new RemoveMembershipCommand(Guid.NewGuid())));
    }

    [Fact]
    public void AnEmptyIdIsInvalid()
    {
        Expect.Invalid(_validator.Validate(new RemoveMembershipCommand(Guid.Empty)), nameof(RemoveMembershipCommand.Id));
    }
}
