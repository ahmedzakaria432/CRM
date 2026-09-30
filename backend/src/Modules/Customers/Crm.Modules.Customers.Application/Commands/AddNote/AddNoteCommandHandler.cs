using System.Globalization;
using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Customers.Domain;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Customers.Application.Commands.AddNote;

public sealed class AddNoteCommandHandler(
    IRepository<Customer, CustomerId> repository,
    ICustomersUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IDateTime dateTime)
    : IRequestHandler<AddNoteCommand, Result<CustomerDto>>
{
    public async Task<Result<CustomerDto>> Handle(AddNoteCommand request, CancellationToken cancellationToken)
    {
        Customer? customer = await repository.GetByIdAsync(new CustomerId(request.CustomerId), cancellationToken);
        if (customer is null)
        {
            return Result.Failure<CustomerDto>(CustomersErrors.CustomerNotFound(request.CustomerId));
        }

        // An unparseable id becomes Guid.Empty, which the aggregate refuses (422 Customers.NoteAuthorRequired).
        Guid authorId = Guid.TryParse(currentUser.UserId, CultureInfo.InvariantCulture, out Guid parsed) ? parsed : Guid.Empty;

        customer.AddNote(request.Body, authorId, dateTime.UtcNow);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(CustomerDto.FromDomain(customer));
    }
}
