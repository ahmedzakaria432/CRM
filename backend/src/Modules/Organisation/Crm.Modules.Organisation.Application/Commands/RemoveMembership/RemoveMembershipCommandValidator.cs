using FluentValidation;

namespace Crm.Modules.Organisation.Application.Commands.RemoveMembership;

public sealed class RemoveMembershipCommandValidator : AbstractValidator<RemoveMembershipCommand>
{
    public RemoveMembershipCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
    }
}
