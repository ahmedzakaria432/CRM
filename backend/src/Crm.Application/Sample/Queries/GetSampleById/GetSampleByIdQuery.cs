// CANONICAL SLICE - copy this shape for every new use case (CLAUDE.md, "Backend", CQRS).
// DELETE this whole Sample folder in the story that lands the first real use case (Story 11).

using Crm.Domain.Common.Results;
using MediatR;

namespace Crm.Application.Sample.Queries.GetSampleById;

public sealed record GetSampleByIdQuery(Guid Id) : IRequest<Result<SampleAggregateDto>>;
