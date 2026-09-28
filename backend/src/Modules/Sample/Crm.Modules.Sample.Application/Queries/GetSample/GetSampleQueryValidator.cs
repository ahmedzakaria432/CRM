// CANONICAL SLICE - copy this shape for every new use case (CLAUDE.md, "Backend", CQRS).
// DELETE this whole Sample module in the story that lands the first real use case (Story 11).

using FluentValidation;

namespace Crm.Modules.Sample.Application.Queries.GetSample;

public sealed class GetSampleQueryValidator : AbstractValidator<GetSampleQuery>
{
    public GetSampleQueryValidator()
    {
        RuleFor(q => q.Name)
            .NotEmpty()
            .MaximumLength(100);
    }
}
