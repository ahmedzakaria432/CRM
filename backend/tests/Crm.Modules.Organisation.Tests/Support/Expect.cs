using Crm.SharedKernel.Results;
using FluentValidation.Results;

namespace Crm.Modules.Organisation.Tests.Support;

public static class Expect
{
    public static void Failure(Result result, string code, ErrorType type)
    {
        ArgumentNullException.ThrowIfNull(result);

        Assert.True(result.IsFailure);
        Assert.Equal(code, result.Error.Code);
        Assert.Equal(type, result.Error.Type);
    }

    public static void Valid(ValidationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}")));
    }

    /// <summary>Invalid, and exactly these properties failed.</summary>
    public static void Invalid(ValidationResult result, params string[] properties)
    {
        ArgumentNullException.ThrowIfNull(result);

        Assert.False(result.IsValid);
        Assert.Equal(properties.OrderBy(p => p, StringComparer.Ordinal), result.Errors.Select(e => e.PropertyName).Distinct().OrderBy(p => p, StringComparer.Ordinal));
    }

    public static async Task DomainCode(Func<Task> action, string code)
    {
        ArgumentNullException.ThrowIfNull(action);

        var exception = await Assert.ThrowsAsync<Crm.SharedKernel.DomainException>(action);

        Assert.Equal(code, exception.Error.Code);
    }
}
