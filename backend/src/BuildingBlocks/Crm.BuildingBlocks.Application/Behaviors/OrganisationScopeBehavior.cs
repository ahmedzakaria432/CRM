using System.Reflection;
using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Exceptions;
using Crm.SharedKernel.Results;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Crm.BuildingBlocks.Application.Behaviors;

/// <summary>Catches OrganisationScopeViolationException (repository read of an out-of-scope row, or the
/// ModuleDbContext write guard) and turns it into a 403 Result, logging every rejection. A request whose
/// TResponse cannot represent a failure (not a Result / Result&lt;T&gt;) lets the exception propagate to
/// GlobalExceptionHandler instead.</summary>
public sealed partial class OrganisationScopeBehavior<TRequest, TResponse>(
    ICurrentUserService currentUser,
    ILogger<OrganisationScopeBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        try
        {
            return await next();
        }
        catch (OrganisationScopeViolationException ex) when (typeof(Result).IsAssignableFrom(typeof(TResponse)))
        {
            LogRejected(typeof(TRequest).Name, ex.Operation, ex.EntityType, currentUser.UserIdOrSystem, currentUser.DepartmentId ?? "-");

            var error = Error.Forbidden("Organisation.OutOfScope", "The record belongs to a department outside your scope.");
            return CreateFailureResponse(error);
        }
    }

    private static TResponse CreateFailureResponse(Error error)
    {
        Type responseType = typeof(TResponse);

        if (responseType == typeof(Result))
        {
            return (TResponse)(object)Result.Failure(error);
        }

        Type valueType = responseType.GetGenericArguments()[0];
        MethodInfo failureMethod = typeof(Result)
            .GetMethod(nameof(Result.Failure), 1, [typeof(Error)])!
            .MakeGenericMethod(valueType);

        return (TResponse)failureMethod.Invoke(null, [error])!;
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Organisation scope rejected {Operation} of {EntityType} in {RequestName} for user {UserId} (departments {Departments})")]
    private partial void LogRejected(string requestName, string operation, string entityType, string userId, string departments);
}
