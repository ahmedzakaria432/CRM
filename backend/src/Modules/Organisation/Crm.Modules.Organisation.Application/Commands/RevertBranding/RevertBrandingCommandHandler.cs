using Crm.BuildingBlocks.Application.Abstractions;
using Crm.SharedKernel.Results;
using Crm.SharedKernel.Scoping;
using MediatR;

namespace Crm.Modules.Organisation.Application.Commands.RevertBranding;

public sealed class RevertBrandingCommandHandler(
    IRepository<Domain.Organisation, TenantId> repository,
    IOrganisationUnitOfWork unitOfWork,
    ICurrentUserService currentUser) : IRequestHandler<RevertBrandingCommand, Result<BrandingSettingsDto>>
{
    public async Task<Result<BrandingSettingsDto>> Handle(RevertBrandingCommand request, CancellationToken cancellationToken)
    {
        Domain.Organisation? organisation = await repository.GetByIdAsync(currentUser.Scope.Tenant, cancellationToken);
        if (organisation is null)
        {
            return Result.Failure<BrandingSettingsDto>(OrganisationErrors.OrganisationNotFound());
        }

        repository.SetOriginalRowVersion(organisation, request.RowVersion);

        // Pre-check for a localised 422; the aggregate throws the same code as the backstop.
        if (!organisation.CanRevertBranding)
        {
            return Result.Failure<BrandingSettingsDto>(OrganisationErrors.NoPreviousBranding());
        }

        organisation.RevertBranding();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(BrandingSettingsDto.FromDomain(organisation));
    }
}
