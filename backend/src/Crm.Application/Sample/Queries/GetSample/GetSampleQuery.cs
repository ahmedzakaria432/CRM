// CANONICAL SLICE - copy this shape for every new use case (CLAUDE.md, "Backend", CQRS).
// DELETE this whole Sample folder in the story that lands the first real use case (Story 11).

using Crm.Domain.Common.Results;
using MediatR;

namespace Crm.Application.Sample.Queries.GetSample;

public sealed record GetSampleQuery(string Name) : IRequest<Result<SampleDto>>;
