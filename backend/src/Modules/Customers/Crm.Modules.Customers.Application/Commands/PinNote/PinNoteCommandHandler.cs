using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Customers.Domain;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Customers.Application.Commands.PinNote;

public sealed class PinNoteCommandHandler(IRepository<Customer, CustomerId> repository, ICustomersUnitOfWork unitOfWork, IDateTime dateTime)
    : IRequestHandler<PinNoteCommand, Result<CustomerDto>>
{
    public async Task<Result<CustomerDto>> Handle(PinNoteCommand request, CancellationToken cancellationToken)
    {
        Customer? customer = await repository.GetByIdAsync(new CustomerId(request.CustomerId), cancellationToken);
        if (customer is null)
        {
            return Result.Failure<CustomerDto>(CustomersErrors.CustomerNotFound(request.CustomerId));
        }

        if (!customer.HasNote(request.NoteId))
        {
            return Result.Failure<CustomerDto>(CustomersErrors.NoteNotFound(request.NoteId));
        }

        if (request.IsPinned)
        {
            customer.PinNote(request.NoteId, dateTime.UtcNow);
        }
        else
        {
            customer.UnpinNote(request.NoteId);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(CustomerDto.FromDomain(customer));
    }
}
