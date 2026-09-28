// CANONICAL SLICE - copy this shape for every new use case (CLAUDE.md, "Backend", CQRS).
// DELETE this whole Sample module in the story that lands the first real use case (Story 11).

using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Sample.Application.Queries.GetSample;

public sealed record GetSampleQuery(string Name) : IRequest<Result<SampleDto>>;
