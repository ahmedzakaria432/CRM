using System.Reflection;
using Crm.Domain.Common.Results;
using FluentValidation;
using FluentValidation.Results;
using MediatR;

namespace Crm.Application.Common.Behaviors;

public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!validators.Any())
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);

        ValidationResult[] validationResults = await Task.WhenAll(
            validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        List<ValidationFailure> failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count == 0)
        {
            return await next();
        }

        if (!typeof(Result).IsAssignableFrom(typeof(TResponse)))
        {
            // TResponse cannot represent a failure (it is not a Result / Result<T>). Silently
            // swallowing validation here would let an unvalidated command reach an aggregate,
            // so this is loud on purpose.
            throw new ValidationException(failures);
        }

        var error = Error.Validation(
            "Validation.Failed",
            string.Join(" | ", failures.Select(f => $"{f.PropertyName}: {f.ErrorMessage}")));

        return CreateFailureResponse(error);
    }

    private static TResponse CreateFailureResponse(Error error)
    {
        Type responseType = typeof(TResponse);

        if (responseType == typeof(Result))
        {
            return (TResponse)(object)Result.Failure(error);
        }

        // TResponse is Result<TValue>. The value type is only known via TResponse's generic argument.
        Type valueType = responseType.GetGenericArguments()[0];
        MethodInfo failureMethod = typeof(Result)
            .GetMethod(nameof(Result.Failure), 1, [typeof(Error)])!
            .MakeGenericMethod(valueType);

        return (TResponse)failureMethod.Invoke(null, [error])!;
    }
}
