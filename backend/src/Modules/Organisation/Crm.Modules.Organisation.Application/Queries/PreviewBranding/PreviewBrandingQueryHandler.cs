using Crm.Modules.Organisation.Application.Theming;
using Crm.Modules.Organisation.Domain;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Organisation.Application.Queries.PreviewBranding;

public sealed class PreviewBrandingQueryHandler : IRequestHandler<PreviewBrandingQuery, Result<BrandingPreviewDto>>
{
    public Task<Result<BrandingPreviewDto>> Handle(PreviewBrandingQuery request, CancellationToken cancellationToken)
    {
        var primary = BrandColour.Create(request.PrimaryColour);
        var accent = BrandColour.Create(request.AccentColour);

        List<ContrastCheckDto> checks = BrandPalette.Evaluate(primary, accent)
            .Select(c => new ContrastCheckDto(c.Name, Math.Round(c.Ratio, 2), c.Required, c.Passes))
            .ToList();

        return Task.FromResult(Result.Success(new BrandingPreviewDto(BrandTokenGenerator.Generate(primary, accent), checks)));
    }
}
