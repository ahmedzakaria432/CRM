// CANONICAL SLICE - copy this shape for every new use case (CLAUDE.md, "Backend", CQRS).
// DELETE this whole Sample module in the story that lands the first real use case (Story 11).

using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Messaging;
using Crm.Modules.Sample.Contracts.IntegrationEvents;
using Crm.Modules.Sample.Domain;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Sample.Application.Commands.RenameSample;

// IDateTime is injected rather than reading the clock directly - DateTime, DateTimeOffset.UtcNow and
// TimeProvider are banned solution-wide, so the clock is only ever reachable through the seam.
// IEventBus.Publish only stages the event: TransactionBehavior writes it to this module's outbox inside the
// command's transaction, so a rename that rolls back publishes nothing.
public sealed class RenameSampleCommandHandler(
    IRepository<SampleAggregate, SampleId> repository,
    ISampleUnitOfWork unitOfWork,
    IDateTime dateTime,
    IEventBus eventBus)
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

        DateTimeOffset now = dateTime.UtcNow;

        repository.SetOriginalRowVersion(sample, request.RowVersion);
        sample.Rename(SampleName.Create(request.Name), now);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        eventBus.Publish(new SampleRenamedIntegrationEvent(Guid.NewGuid(), now, sample.Id.Value, sample.Name.Value));

        return Result.Success(SampleAggregateDto.FromDomain(sample));
    }
}
