// CANONICAL SLICE - copy this shape for every new use case (CLAUDE.md, "Backend", CQRS).
// DELETE this whole Sample module in the story that lands the first real use case (Story 11).

using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Sample.Application.Queries.ListSamples;

public sealed record ListSamplesQuery : IRequest<Result<IReadOnlyList<SampleAggregateDto>>>;
