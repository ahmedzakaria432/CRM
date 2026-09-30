using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Organisation.Contracts;
using Crm.Modules.Tickets.Domain;
using Crm.SharedKernel.Results;
using Crm.SharedKernel.Scoping;
using MediatR;

namespace Crm.Modules.Tickets.Application.Commands.CreateCategory;

public sealed class CreateCategoryCommandHandler(
    IRepository<Category, CategoryId> categories,
    ITicketsUnitOfWork unitOfWork,
    IOrganisationModuleApi organisation)
    : IRequestHandler<CreateCategoryCommand, Result<TicketCategoryDto>>
{
    public async Task<Result<TicketCategoryDto>> Handle(CreateCategoryCommand request, CancellationToken cancellationToken)
    {
        Result<DepartmentReference> department = await organisation.GetDepartmentAsync(request.DepartmentId, cancellationToken);
        if (department.IsFailure)
        {
            return Result.Failure<TicketCategoryDto>(department.Error);
        }

        if (!department.Value.IsActive)
        {
            return Result.Failure<TicketCategoryDto>(TicketsErrors.CategoryDepartmentInactive());
        }

        Category category = Category.Create(
            new TenantId(department.Value.TenantId), new DepartmentId(department.Value.Id),
            LocalizedText.Create(request.NameAr, request.NameEn), request.DisplayOrder, request.IsCustomerVisible);

        categories.Add(category);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(TicketCategoryDto.FromDomain(category));
    }
}
