using MediatR;

namespace Crm.Application.Common.Behaviors;

// Story 02 - Persistence baseline with auditing and soft delete.
// This is a pass-through placeholder until there is a DbContext to open a transaction on.
public sealed class TransactionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
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
