// CANONICAL SLICE - copy this shape for every new use case (CLAUDE.md, "Backend", CQRS).
// DELETE this whole Sample folder in the story that lands the first real use case (Story 11).

using Crm.Application.Common.Abstractions;
using Crm.Domain.Common.Results;
using Crm.Domain.Sample;
using MediatR;

namespace Crm.Application.Sample.Commands.CreateSample;

public sealed class CreateSampleCommandHandler(
    IRepository<SampleAggregate, SampleId> repository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CreateSampleCommand, Result<SampleAggregateDto>>
{
    public async Task<Result<SampleAggregateDto>> Handle(CreateSampleCommand request, CancellationToken cancellationToken)
    {
        SampleAggregate sample = SampleAggregate.Create(SampleName.Create(request.Name), request.Lines);

        repository.Add(sample);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(SampleAggregateDto.FromDomain(sample));
    }
}
