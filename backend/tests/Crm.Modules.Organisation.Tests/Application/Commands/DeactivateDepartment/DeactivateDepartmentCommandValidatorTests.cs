using Crm.Modules.Organisation.Application.Commands.DeactivateDepartment;
using Crm.Modules.Organisation.Tests.Support;

namespace Crm.Modules.Organisation.Tests.Application.Commands.DeactivateDepartment;

public sealed class DeactivateDepartmentCommandValidatorTests
{
    private readonly DeactivateDepartmentCommandValidator _validator = new();

    [Fact]
    public void AValidCommandPasses()
    {
        Expect.Valid(_validator.Validate(new DeactivateDepartmentCommand(Guid.NewGuid(), Ids.RowVersion())));
    }

    [Fact]
    public void AnEmptyIdFails()
    {
        Expect.Invalid(_validator.Validate(new DeactivateDepartmentCommand(Guid.Empty, Ids.RowVersion())), nameof(DeactivateDepartmentCommand.Id));
    }

    [Fact]
    public void AnEmptyRowVersionFails()
    {
        Expect.Invalid(_validator.Validate(new DeactivateDepartmentCommand(Guid.NewGuid(), [])), nameof(DeactivateDepartmentCommand.RowVersion));
    }
}
