using FluentValidation;

namespace Crm.Modules.Organisation.Application.Commands.UploadBrandLogo;

public sealed class UploadBrandLogoCommandValidator : AbstractValidator<UploadBrandLogoCommand>
{
    public UploadBrandLogoCommandValidator()
    {
        RuleFor(c => c.Content).NotNull();
    }
}
