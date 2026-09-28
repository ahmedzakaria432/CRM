// CANONICAL SLICE - copy this shape for every new use case (CLAUDE.md, "Backend", CQRS).
// DELETE this whole Sample module in the story that lands the first real use case (Story 11).

using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Sample.Domain;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Sample.Application.Commands.RenameSample;

// IDateTime is injected rather than reading the clock directly - DateTime, DateTimeOffset.UtcNow and
// TimeProvider are banned solution-wide, so the clock is only ever reachable through the seam.
public sealed class RenameSampleCommandHandler(
    IRepository<SampleAggregate, SampleId> repository,
    ISampleUnitOfWork unitOfWork,
    IDateTime dateTime)
    : IRequestHandler<RenameSampleCommand, Result<SampleAggregateDto>>
{
    public async Task<Result<SampleAggregateDto>> Handle(RenameSampleCommand request, CancellationToken cancellationToken)
    {
        var id = new SampleId(request.Id);
        SampleAggregate? sample = await repository.GetByIdAsync(id, cancellationToken);

        if (sample is null)
        {
            return Result.Failure<SampleAggregateDto>(Error.NotFound("Sample.NotFound", $"Sample '{request.Id}' was not found."));
        }

        repository.SetOriginalRowVersion(sample, request.RowVersion);
        sample.Rename(SampleName.Create(request.Name), dateTime.UtcNow);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(SampleAggregateDto.FromDomain(sample));
    }
}
