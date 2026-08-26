// CANONICAL SLICE - copy this shape for every new use case (CLAUDE.md, "Backend", CQRS).
// DELETE this whole Sample folder in the story that lands the first real use case (Story 11).

using Crm.Domain.Common.Results;
using MediatR;

namespace Crm.Application.Sample.Queries.GetSample;

public sealed class GetSampleQueryHandler : IRequestHandler<GetSampleQuery, Result<SampleDto>>
{
    public Task<Result<SampleDto>> Handle(GetSampleQuery request, CancellationToken cancellationToken)
    {
        var dto = new SampleDto(request.Name, DateTimeOffset.UtcNow);
        return Task.FromResult(Result.Success(dto));
    }
}
