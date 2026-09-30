using Crm.Modules.Organisation.Domain;
using FluentValidation;

namespace Crm.Modules.Organisation.Application.Commands.AddMembership;

public sealed class AddMembershipCommandValidator : AbstractValidator<AddMembershipCommand>
{
    public AddMembershipCommandValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();
        RuleFor(c => c.DepartmentId).NotEmpty();
        RuleFor(c => c.Role)
            .Must(r => Enum.TryParse<SystemRole>(r, false, out _))
            .WithMessage("Role must be Administrator, Supervisor, or Agent.");
    }
}
