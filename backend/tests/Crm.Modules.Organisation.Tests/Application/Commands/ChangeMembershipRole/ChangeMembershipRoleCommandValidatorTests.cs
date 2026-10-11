using Crm.Modules.Organisation.Application.Commands.ChangeMembershipRole;
using Crm.Modules.Organisation.Tests.Support;

namespace Crm.Modules.Organisation.Tests.Application.Commands.ChangeMembershipRole;

public sealed class ChangeMembershipRoleCommandValidatorTests
{
    private readonly ChangeMembershipRoleCommandValidator _validator = new();

    [Fact]
    public void AValidCommandPasses()
    {
        Expect.Valid(_validator.Validate(new ChangeMembershipRoleCommand(Guid.NewGuid(), "Supervisor", Ids.RowVersion())));
    }

    [Fact]
    public void AnEmptyIdIsInvalid()
    {
        Expect.Invalid(_validator.Validate(new ChangeMembershipRoleCommand(Guid.Empty, "Agent", Ids.RowVersion())), nameof(ChangeMembershipRoleCommand.Id));
    }

    [Theory]
    [InlineData("")]
    [InlineData("owner")]
    [InlineData("agent")]
    public void ABadRoleIsInvalid(string role)
    {
        Expect.Invalid(_validator.Validate(new ChangeMembershipRoleCommand(Guid.NewGuid(), role, Ids.RowVersion())), nameof(ChangeMembershipRoleCommand.Role));
    }

    [Fact]
    public void AnEmptyRowVersionIsInvalid()
    {
        Expect.Invalid(_validator.Validate(new ChangeMembershipRoleCommand(Guid.NewGuid(), "Agent", [])), nameof(ChangeMembershipRoleCommand.RowVersion));
    }
}
