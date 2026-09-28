using MediatR;

namespace Crm.BuildingBlocks.Application.Behaviors;

// Story 09 - Permission model and policy based authorisation.
// This is a pass-through placeholder until the [RequirePermission] pipeline lands.
public sealed class AuthorizationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        return next();
    }
}
