using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Tickets.Domain;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Tickets.Application.Queries.GetCategory;

public sealed class GetCategoryQueryHandler(IRepository<Category, CategoryId> categories)
    : IRequestHandler<GetCategoryQuery, Result<TicketCategoryDto>>
{
    public async Task<Result<TicketCategoryDto>> Handle(GetCategoryQuery request, CancellationToken cancellationToken)
    {
        Category? category = await categories.GetByIdAsync(new CategoryId(request.Id), cancellationToken);

        return category is null
            ? Result.Failure<TicketCategoryDto>(TicketsErrors.CategoryNotFound(request.Id))
            : Result.Success(TicketCategoryDto.FromDomain(category));
    }
}
