using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Customers.Domain;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Customers.Application.Commands.RemoveNote;

public sealed class RemoveNoteCommandHandler(IRepository<Customer, CustomerId> repository, ICustomersUnitOfWork unitOfWork)
    : IRequestHandler<RemoveNoteCommand, Result<CustomerDto>>
{
    public async Task<Result<CustomerDto>> Handle(RemoveNoteCommand request, CancellationToken cancellationToken)
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

        customer.RemoveNote(request.NoteId);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(CustomerDto.FromDomain(customer));
    }
}
