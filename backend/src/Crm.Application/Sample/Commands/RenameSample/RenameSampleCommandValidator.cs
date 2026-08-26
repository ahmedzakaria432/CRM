// CANONICAL SLICE - copy this shape for every new use case (CLAUDE.md, "Backend", CQRS).
// DELETE this whole Sample folder in the story that lands the first real use case (Story 11).

using FluentValidation;

namespace Crm.Application.Sample.Commands.RenameSample;

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
