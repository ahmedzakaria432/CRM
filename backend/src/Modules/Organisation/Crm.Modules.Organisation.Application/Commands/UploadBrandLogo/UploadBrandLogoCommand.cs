using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Messaging;

namespace Crm.Modules.Organisation.Application.Commands.UploadBrandLogo;

/// <summary>Carries the file bytes: the request is only destructured by LoggingBehavior at Debug, which is off everywhere.</summary>
[RequirePermission(CrmPermissions.AdminBranding)]
public sealed record UploadBrandLogoCommand(byte[] Content) : ICommand<BrandLogoDto>;
