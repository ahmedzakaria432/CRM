using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Tickets.Domain;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Tickets.Application.Commands.UpdateCategory;

public sealed class UpdateCategoryCommandHandler(IRepository<Category, CategoryId> categories, ITicketsUnitOfWork unitOfWork)
    : IRequestHandler<UpdateCategoryCommand, Result<TicketCategoryDto>>
{
    public async Task<Result<TicketCategoryDto>> Handle(UpdateCategoryCommand request, CancellationToken cancellationToken)
    {
        Category? category = await categories.GetByIdAsync(new CategoryId(request.Id), cancellationToken);
        if (category is null)
        {
            return Result.Failure<TicketCategoryDto>(TicketsErrors.CategoryNotFound(request.Id));
        }

        categories.SetOriginalRowVersion(category, request.RowVersion);
        category.Update(LocalizedText.Create(request.NameAr, request.NameEn), request.DisplayOrder, request.IsCustomerVisible);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(TicketCategoryDto.FromDomain(category));
    }
}
