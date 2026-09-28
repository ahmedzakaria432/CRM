// CANONICAL SLICE - copy this shape for every new use case (CLAUDE.md, "Backend", CQRS).
// DELETE this whole Sample module in the story that lands the first real use case (Story 11).

using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Sample.Application.Specifications;
using Crm.Modules.Sample.Domain;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Sample.Application.Queries.GetSampleById;

public sealed class GetSampleByIdQueryHandler(IRepository<SampleAggregate, SampleId> repository)
    : IRequestHandler<GetSampleByIdQuery, Result<SampleAggregateDto>>
{
    public async Task<Result<SampleAggregateDto>> Handle(GetSampleByIdQuery request, CancellationToken cancellationToken)
    {
        SampleAggregate? sample = await repository.FirstOrDefaultAsync(
            new SampleByIdSpecification(new SampleId(request.Id)), cancellationToken);

        return sample is null
            ? Result.Failure<SampleAggregateDto>(Error.NotFound("Sample.NotFound", $"Sample '{request.Id}' was not found."))
            : Result.Success(SampleAggregateDto.FromDomain(sample));
    }
}
