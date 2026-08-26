// CANONICAL SLICE - copy this shape for every new use case (CLAUDE.md, "Backend", CQRS).
// DELETE this whole Sample folder in the story that lands the first real use case (Story 11).

using Crm.Application.Common.Abstractions;
using Crm.Application.Sample.Specifications;
using Crm.Domain.Common.Results;
using Crm.Domain.Sample;
using MediatR;

namespace Crm.Application.Sample.Queries.GetSampleById;

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
