// CANONICAL SLICE - copy this shape for every new use case (CLAUDE.md, "Backend", CQRS).
// DELETE this whole Sample module in the story that lands the first real use case (Story 11).

using Crm.BuildingBlocks.Application.Abstractions;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Sample.Application.Queries.GetSample;

// IDateTime is injected rather than reading the clock directly - DateTime, DateTimeOffset.UtcNow and
// TimeProvider are banned solution-wide, so the clock is only ever reachable through the seam.
public sealed class GetSampleQueryHandler(IDateTime dateTime) : IRequestHandler<GetSampleQuery, Result<SampleDto>>
{
    public Task<Result<SampleDto>> Handle(GetSampleQuery request, CancellationToken cancellationToken)
    {
        var dto = new SampleDto(request.Name, dateTime.UtcNow);
        return Task.FromResult(Result.Success(dto));
    }
}
