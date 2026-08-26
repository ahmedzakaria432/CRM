// CANONICAL SLICE - copy this shape for every new use case (CLAUDE.md, "Backend", CQRS).
// DELETE this whole Sample folder in the story that lands the first real use case (Story 11).

using Crm.Domain.Common.Results;
using MediatR;

namespace Crm.Application.Sample.Queries.GetSample;

// TimeProvider is injected rather than reading DateTimeOffset.UtcNow directly - that read is
// banned solution-wide (Story 02 Task 11) so the clock is only ever reachable through the seam.
public sealed class GetSampleQueryHandler(TimeProvider timeProvider) : IRequestHandler<GetSampleQuery, Result<SampleDto>>
{
    public Task<Result<SampleDto>> Handle(GetSampleQuery request, CancellationToken cancellationToken)
    {
        var dto = new SampleDto(request.Name, timeProvider.GetUtcNow());
        return Task.FromResult(Result.Success(dto));
    }
}
