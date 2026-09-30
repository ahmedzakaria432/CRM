// CANONICAL SLICE - copy this shape for every new use case (CLAUDE.md, "Backend", CQRS).
// DELETE this whole Sample module in the story that lands the first real use case (Story 11).

using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Sample.Application.Specifications;
using Crm.Modules.Sample.Domain;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Sample.Application.Queries.ListSamples;

public sealed class ListSamplesQueryHandler(IRepository<SampleAggregate, SampleId> repository)
    : IRequestHandler<ListSamplesQuery, Result<IReadOnlyList<SampleAggregateDto>>>
{
    public async Task<Result<IReadOnlyList<SampleAggregateDto>>> Handle(ListSamplesQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<SampleAggregate> samples = await repository.ListAsync(new SamplesSpecification(), cancellationToken);
        return Result.Success<IReadOnlyList<SampleAggregateDto>>(samples.Select(SampleAggregateDto.FromDomain).ToList());
    }
}
