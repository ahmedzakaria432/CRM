using Crm.Modules.Organisation.Application.Commands.DeactivateBranch;
using Crm.Modules.Organisation.Tests.Support;

namespace Crm.Modules.Organisation.Tests.Application.Commands.DeactivateBranch;

public sealed class DeactivateBranchCommandValidatorTests
{
    private readonly DeactivateBranchCommandValidator _validator = new();

    [Fact]
    public void AValidCommandPasses()
    {
        Expect.Valid(_validator.Validate(new DeactivateBranchCommand(Guid.NewGuid(), Ids.RowVersion())));
    }

    [Fact]
    public void AnEmptyIdFails()
    {
        Expect.Invalid(_validator.Validate(new DeactivateBranchCommand(Guid.Empty, Ids.RowVersion())), nameof(DeactivateBranchCommand.Id));
    }

    [Fact]
    public void AnEmptyRowVersionFails()
    {
        Expect.Invalid(_validator.Validate(new DeactivateBranchCommand(Guid.NewGuid(), [])), nameof(DeactivateBranchCommand.RowVersion));
    }
}
