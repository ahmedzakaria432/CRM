// CANONICAL SLICE - copy this shape for every new use case (CLAUDE.md, "Backend", CQRS).
// DELETE this endpoint group in the story that lands the first real use case (Story 11).

using Crm.Api.Common;
using Crm.Application.Sample.Queries.GetSample;
using Crm.Domain.Common.Results;
using MediatR;

namespace Crm.Api.Endpoints;

public sealed class SampleEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/sample", async (string? name, ISender sender, CancellationToken cancellationToken) =>
        {
            Result<SampleDto> result = await sender.Send(new GetSampleQuery(name ?? string.Empty), cancellationToken);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : result.Error.ToProblem();
        })
        .WithName("GetSample")
        .WithTags("Sample");
    }
}
