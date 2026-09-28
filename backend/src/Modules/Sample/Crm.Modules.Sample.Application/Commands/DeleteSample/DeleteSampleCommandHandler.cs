// CANONICAL SLICE - copy this shape for every new use case (CLAUDE.md, "Backend", CQRS).
// DELETE this whole Sample module in the story that lands the first real use case (Story 11).

using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Sample.Domain;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Sample.Application.Commands.DeleteSample;

// Remove() is the soft-delete entry point - AuditingSaveChangesInterceptor turns this EF delete
// into a flag update, so this handler needs no knowledge that the delete is "soft" at all.
public sealed class DeleteSampleCommandHandler(
    IRepository<SampleAggregate, SampleId> repository,
    ISampleUnitOfWork unitOfWork)
    : IRequestHandler<DeleteSampleCommand, Result>
{
    public async Task<Result> Handle(DeleteSampleCommand request, CancellationToken cancellationToken)
    {
        var id = new SampleId(request.Id);
        SampleAggregate? sample = await repository.GetByIdAsync(id, cancellationToken);

        if (sample is null)
        {
            return Result.Failure(Error.NotFound("Sample.NotFound", $"Sample '{request.Id}' was not found."));
        }

        repository.Remove(sample);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
