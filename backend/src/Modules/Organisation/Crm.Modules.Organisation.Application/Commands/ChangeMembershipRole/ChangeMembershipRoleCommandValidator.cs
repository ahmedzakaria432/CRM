using Crm.Modules.Organisation.Domain;
using FluentValidation;

namespace Crm.Modules.Organisation.Application.Commands.ChangeMembershipRole;

public sealed class ChangeMembershipRoleCommandValidator : AbstractValidator<ChangeMembershipRoleCommand>
{
    public ChangeMembershipRoleCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.Role)
            .Must(r => Enum.TryParse<SystemRole>(r, false, out _))
            .WithMessage("Role must be Administrator, Supervisor, or Agent.");
        RuleFor(c => c.RowVersion).NotEmpty();
    }
}
