// CANONICAL SLICE - copy this shape for every new use case (CLAUDE.md, "Backend", CQRS).
// DELETE this whole Sample folder in the story that lands the first real use case (Story 11).

using Crm.Application.Common.Abstractions;
using Crm.Domain.Common.Results;
using Crm.Domain.Sample;
using MediatR;

namespace Crm.Application.Sample.Commands.RenameSample;

// TimeProvider is injected rather than reading DateTimeOffset.UtcNow directly - that read is
// banned solution-wide (Story 02 Task 11) so the clock is only ever reachable through the seam.
public sealed class RenameSampleCommandHandler(
    IRepository<SampleAggregate, SampleId> repository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
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
        sample.Rename(SampleName.Create(request.Name), timeProvider.GetUtcNow());

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(SampleAggregateDto.FromDomain(sample));
    }
}
