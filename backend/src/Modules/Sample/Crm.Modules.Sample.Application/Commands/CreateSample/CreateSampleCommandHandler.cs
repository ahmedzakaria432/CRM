// CANONICAL SLICE - copy this shape for every new use case (CLAUDE.md, "Backend", CQRS).
// DELETE this whole Sample module in the story that lands the first real use case (Story 11).

using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Sample.Domain;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Sample.Application.Commands.CreateSample;

public sealed class CreateSampleCommandHandler(
    IRepository<SampleAggregate, SampleId> repository,
    ISampleUnitOfWork unitOfWork)
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
