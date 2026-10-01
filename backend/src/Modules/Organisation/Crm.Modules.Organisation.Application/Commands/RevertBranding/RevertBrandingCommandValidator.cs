using FluentValidation;

namespace Crm.Modules.Organisation.Application.Commands.RevertBranding;

public sealed class RevertBrandingCommandValidator : AbstractValidator<RevertBrandingCommand>
{
    public RevertBrandingCommandValidator()
    {
        RuleFor(c => c.RowVersion).NotEmpty();
    }
}
