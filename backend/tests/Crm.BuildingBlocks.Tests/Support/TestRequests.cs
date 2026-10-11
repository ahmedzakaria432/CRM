using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Caching;
using Crm.BuildingBlocks.Application.Messaging;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.BuildingBlocks.Tests.Support;

// MediatR fixtures. AuthorizationBehavior and CachingBehavior keep per-closed-generic statics, so a scenario that
// must differ (a non-Result response, a different attribute) gets its own request type. None of these assemblies
// is ever handed to RequestAuthorizationGuard.EnsureEveryRequestDeclares.

[RequirePermission(CrmPermissions.TicketsRead)]
public sealed record ReadQuery : IRequest<Result<int>>;

[RequirePermission(CrmPermissions.TicketsWrite)]
public sealed record WriteCommand : ICommand;

[RequirePermission(CrmPermissions.TicketsAssign)]
public sealed record AssignCommand : ICommand;

[RequirePermission(CrmPermissions.AdminOrganisation)]
public sealed record AdminCommand : ICommand;

[RequireAuthenticatedUser]
public sealed record ValueCommand : ICommand<int>;

[RequireAuthenticatedUser]
public sealed record AuthenticatedOnlyQuery : IRequest<Result<int>>;

[RequireAuthenticatedUser]
public sealed record AuthenticatedOnlyCommand : ICommand;

[AllowAnonymousRequest]
public sealed record AnonymousCommand : ICommand;

[RequirePermission(CrmPermissions.PortalTickets)]
public sealed record PortalTicketsQuery : IRequest<Result<int>>;

[RequireAuthenticatedUser]
public sealed record NonResultRequest : IRequest<string>;

[RequireAuthenticatedUser]
public sealed record ValidatedCommand(string Name) : ICommand;

[RequireAuthenticatedUser]
public sealed record ValidatedValueCommand(string Name) : ICommand<int>;

[RequireAuthenticatedUser]
public sealed record ValidatedNonResultRequest(string Name) : IRequest<string>;

[RequireAuthenticatedUser]
public sealed record CacheableQuery(string Key = "item:1") : IRequest<Result<int>>, ICacheableQuery
{
    public string CacheKey => Key;

    public IReadOnlyCollection<string> CacheTags => ["customers:customers:1"];
}

[RequireAuthenticatedUser]
public sealed record CacheableUserQuery : IRequest<Result<int>>, ICacheableQuery
{
    public string CacheKey => "me";

    public IReadOnlyCollection<string> CacheTags => ["customers:customers:me"];

    public bool VaryByUser => true;
}

[RequireAuthenticatedUser]
public sealed record CacheableTimedQuery : IRequest<Result<int>>, ICacheableQuery
{
    public string CacheKey => "timed";

    public IReadOnlyCollection<string> CacheTags => ["a", "b"];

    public TimeSpan? CacheDuration => TimeSpan.FromMinutes(42);
}

[RequireAuthenticatedUser]
public sealed record CacheableNonResultQuery : IRequest<string>, ICacheableQuery
{
    public string CacheKey => "non-result";

    public IReadOnlyCollection<string> CacheTags => [];
}

[RequireAuthenticatedUser]
public sealed record CacheableRequiredOnlyQuery : IRequest<Result<int>>, ICacheableQuery
{
    public string CacheKey => "defaults";

    public IReadOnlyCollection<string> CacheTags => [];
}

[RequireAuthenticatedUser]
public sealed record InvalidatingCommand : ICommand, ICacheInvalidatingCommand
{
    public IReadOnlyCollection<string> CacheTagsToInvalidate => ["customers:customers:1", "customers:customers:list"];
}

[RequireAuthenticatedUser]
public sealed record InvalidatingValueCommand : ICommand<int>, ICacheInvalidatingCommand
{
    public IReadOnlyCollection<string> CacheTagsToInvalidate => ["customers:customers:1"];
}
