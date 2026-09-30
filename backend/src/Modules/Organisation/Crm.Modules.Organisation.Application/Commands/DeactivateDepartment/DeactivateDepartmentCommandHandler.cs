using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Organisation.Domain;
using Crm.SharedKernel.Results;
using Crm.SharedKernel.Scoping;
using MediatR;

namespace Crm.Modules.Organisation.Application.Commands.DeactivateDepartment;

public sealed class DeactivateDepartmentCommandHandler(IRepository<Department, DepartmentId> repository, IOrganisationUnitOfWork unitOfWork)
    : IRequestHandler<DeactivateDepartmentCommand, Result<DepartmentDto>>
{
    public async Task<Result<DepartmentDto>> Handle(DeactivateDepartmentCommand request, CancellationToken cancellationToken)
    {
        var id = new DepartmentId(request.Id);
        Department? department = await repository.GetByIdAsync(id, cancellationToken);

        if (department is null)
        {
            return Result.Failure<DepartmentDto>(Error.NotFound("Organisation.DepartmentNotFound", $"Department '{request.Id}' was not found."));
        }

        repository.SetOriginalRowVersion(department, request.RowVersion);
        department.Deactivate();

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(DepartmentDto.FromDomain(department));
    }
}
