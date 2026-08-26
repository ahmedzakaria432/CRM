// CANONICAL SLICE - copy this shape for every new use case (CLAUDE.md, "Backend", CQRS).
// DELETE this whole Sample folder in the story that lands the first real use case (Story 11).

using Crm.Application.Common.Abstractions;
using Crm.Domain.Common.Results;
using Crm.Domain.Sample;
using MediatR;

namespace Crm.Application.Sample.Commands.DeleteSample;

// Remove() is the soft-delete entry point - AuditingSaveChangesInterceptor turns this EF delete
// into a flag update, so this handler needs no knowledge that the delete is "soft" at all.
public sealed class DeleteSampleCommandHandler(
    IRepository<SampleAggregate, SampleId> repository,
    IUnitOfWork unitOfWork)
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
