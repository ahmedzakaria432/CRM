using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Tickets.Domain;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Tickets.Application.Commands.DeactivateCategory;

public sealed class DeactivateCategoryCommandHandler(IRepository<Category, CategoryId> categories, ITicketsUnitOfWork unitOfWork)
    : IRequestHandler<DeactivateCategoryCommand, Result<TicketCategoryDto>>
{
    public async Task<Result<TicketCategoryDto>> Handle(DeactivateCategoryCommand request, CancellationToken cancellationToken)
    {
        Category? category = await categories.GetByIdAsync(new CategoryId(request.Id), cancellationToken);
        if (category is null)
        {
            return Result.Failure<TicketCategoryDto>(TicketsErrors.CategoryNotFound(request.Id));
        }

        categories.SetOriginalRowVersion(category, request.RowVersion);
        category.Deactivate();

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(TicketCategoryDto.FromDomain(category));
    }
}
