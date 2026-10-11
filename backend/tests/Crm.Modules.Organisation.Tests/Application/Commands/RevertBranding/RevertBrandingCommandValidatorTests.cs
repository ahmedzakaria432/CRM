using Crm.Modules.Organisation.Application.Commands.RevertBranding;
using Crm.Modules.Organisation.Tests.Support;

namespace Crm.Modules.Organisation.Tests.Application.Commands.RevertBranding;

public sealed class RevertBrandingCommandValidatorTests
{
    private readonly RevertBrandingCommandValidator _validator = new();

    [Fact]
    public void ARowVersionIsValid()
    {
        Expect.Valid(_validator.Validate(new RevertBrandingCommand(Ids.RowVersion())));
    }

    [Fact]
    public void AnEmptyRowVersionFails()
    {
        Expect.Invalid(_validator.Validate(new RevertBrandingCommand([])), nameof(RevertBrandingCommand.RowVersion));
    }
}
