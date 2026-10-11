using Crm.SharedKernel.Results;

namespace Crm.SharedKernel.Tests.Results;

public sealed class ResultOfTTests
{
    private static readonly Error SomeError = Error.NotFound("Thing.NotFound", "No such thing.");

    [Fact]
    public void ValueIsReturnedOnSuccess()
    {
        var result = Result.Success("hello");

        Assert.Equal("hello", result.Value);
    }

    [Fact]
    public void ValueOnFailureThrowsAndNamesTheErrorCode()
    {
        var result = Result.Failure<string>(SomeError);

        var exception = Assert.Throws<InvalidOperationException>(() => result.Value);

        Assert.Contains("Thing.NotFound", exception.Message, StringComparison.Ordinal);
        Assert.Contains("No such thing.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ImplicitConversionFromValueIsASuccess()
    {
        Result<int> result = 7;

        Assert.True(result.IsSuccess);
        Assert.Equal(7, result.Value);
    }

    [Fact]
    public void ReferenceTypeSuccessKeepsTheSameInstance()
    {
        var payload = new object();

        var result = Result.Success(payload);

        Assert.Same(payload, result.Value);
    }

    [Fact]
    public void NullReferenceSuccessIsStillASuccess()
    {
        var result = Result.Success<string?>(null);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
    }

    [Fact]
    public void DefaultValueSuccessIsStillASuccess()
    {
        var result = Result.Success(0);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value);
    }
}
