using System.Reflection;
using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Caching;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.BuildingBlocks.Application.Behaviors;

/// <summary>
/// Caches the successful result of an <see cref="ICacheableQuery"/>. Last in the pipeline, so validation and
/// authorisation run on every call - a cache hit never skips a permission check. Failures are never cached.
/// </summary>
public sealed class CachingBehavior<TRequest, TResponse>(
    ICacheService cache,
    ICurrentUserService currentUser,
    CachingSettings settings)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    // TResponse is Result<TValue>, but TValue is only known through reflection - the same technique
    // ValidationBehavior uses. Resolved once per closed behaviour type.
    private static readonly MethodInfo? HandleCachedMethod =
        typeof(TResponse).IsGenericType && typeof(TResponse).GetGenericTypeDefinition() == typeof(Result<>)
            ? (typeof(CachingBehavior<TRequest, TResponse>)
                    .GetMethod(nameof(HandleCachedAsync), BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException($"{nameof(HandleCachedAsync)} not found."))
                .MakeGenericMethod(typeof(TResponse).GetGenericArguments()[0])
            : null;

    public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        if (request is not ICacheableQuery cacheable)
        {
            return next();
        }

        if (HandleCachedMethod is null)
        {
            // Loud on purpose, like ValidationBehavior: a cacheable query that returns something else would
            // silently never be cached.
            throw new InvalidOperationException(
                $"'{typeof(TRequest).Name}' implements ICacheableQuery but returns '{typeof(TResponse).Name}'. A cacheable query must return Result<T>.");
        }

        return HandleCachedMethod.Invoke(this, [cacheable, next, cancellationToken]) as Task<TResponse>
            ?? throw new InvalidOperationException($"{nameof(HandleCachedAsync)} returned no task.");
    }

    private async Task<TResponse> HandleCachedAsync<TValue>(
        ICacheableQuery cacheable,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        TResponse? failure = default;

        CachedValue<TValue>? cached = await cache.GetOrCreateAsync<CachedValue<TValue>>(
            BuildKey(cacheable),
            async _ =>
            {
                TResponse response = await next();
                if (response is Result<TValue> { IsSuccess: true } success)
                {
                    return new CachedValue<TValue>(success.Value);
                }

                failure = response;
                return null;
            },
            new CacheEntrySettings(cacheable.CacheDuration ?? settings.DefaultExpiration, cacheable.CacheTags),
            cancellationToken);

        if (cached is not null)
        {
            return (TResponse)(object)Result.Success(cached.Value);
        }

        // The factory returns null only after storing the failed response it refused to cache.
        return failure ?? throw new InvalidOperationException("The cache returned no value and the query produced no failure.");
    }

    // Without the scope prefix, one tenant's cached data would be served to another once Story 06 fills the
    // scope claims. Invalidation is by tag, which is scope-free, so a change invalidates every scope's copy.
    private string BuildKey(ICacheableQuery cacheable)
    {
        string scope = $"t={currentUser.TenantId ?? "-"}|d={currentUser.DepartmentId ?? "-"}|b={currentUser.BranchId ?? "-"}";

        if (cacheable.VaryByUser)
        {
            scope += $"|u={currentUser.UserIdOrSystem}";
        }

        return $"{scope}:{cacheable.CacheKey}";
    }
}

/// <summary>Wraps the value so value-type results satisfy ICacheService's class constraint.</summary>
public sealed record CachedValue<TValue>(TValue Value);
