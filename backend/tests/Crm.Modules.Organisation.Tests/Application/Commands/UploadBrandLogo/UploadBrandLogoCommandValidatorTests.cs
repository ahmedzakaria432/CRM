using Crm.Modules.Organisation.Application.Commands.UploadBrandLogo;
using Crm.Modules.Organisation.Tests.Support;

namespace Crm.Modules.Organisation.Tests.Application.Commands.UploadBrandLogo;

public sealed class UploadBrandLogoCommandValidatorTests
{
    private readonly UploadBrandLogoCommandValidator _validator = new();

    [Fact]
    public void ContentIsValid()
    {
        Expect.Valid(_validator.Validate(new UploadBrandLogoCommand(Logos.PngBytes())));
    }

    [Fact]
    public void AnEmptyArrayIsTheDomainsConcernAndPassesTheValidator()
    {
        Expect.Valid(_validator.Validate(new UploadBrandLogoCommand([])));
    }

    [Fact]
    public void NullContentIsInvalid()
    {
        // A literal null does not compile warning-free for a non-nullable parameter, so build the command by reflection.
        var command = (UploadBrandLogoCommand)typeof(UploadBrandLogoCommand).GetConstructors()[0].Invoke([null]);

        Expect.Invalid(_validator.Validate(command), nameof(UploadBrandLogoCommand.Content));
    }
}
