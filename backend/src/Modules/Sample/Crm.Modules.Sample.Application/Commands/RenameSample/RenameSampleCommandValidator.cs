// CANONICAL SLICE - copy this shape for every new use case (CLAUDE.md, "Backend", CQRS).
// DELETE this whole Sample module in the story that lands the first real use case (Story 11).

using FluentValidation;

namespace Crm.Modules.Sample.Application.Commands.RenameSample;

public sealed class RenameSampleCommandValidator : AbstractValidator<RenameSampleCommand>
{
    public RenameSampleCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();

        RuleFor(c => c.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(c => c.RowVersion).NotEmpty();
    }
}
