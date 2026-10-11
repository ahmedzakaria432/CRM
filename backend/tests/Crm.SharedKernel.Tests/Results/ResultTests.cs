using Crm.SharedKernel.Results;

namespace Crm.SharedKernel.Tests.Results;

public sealed class ResultTests
{
    private static readonly Error SomeError = Error.Failure("Test.Failure", "Something went wrong.");

    [Fact]
    public void SuccessHasNoError()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(Error.None, result.Error);
    }

    [Fact]
    public void FailureCarriesItsError()
    {
        var result = Result.Failure(SomeError);

        Assert.True(result.IsFailure);
        Assert.Equal(SomeError, result.Error);
    }

    [Fact]
    public void FailureWithoutAnErrorIsRejected()
    {
        Assert.Throws<InvalidOperationException>(() => Result.Failure(Error.None));
    }

    [Fact]
    public void IsFailureMirrorsIsSuccess()
    {
        Assert.False(Result.Success().IsFailure);
        Assert.True(Result.Failure(SomeError).IsFailure);
        Assert.False(Result.Failure(SomeError).IsSuccess);
    }

    [Fact]
    public void SuccessCannotCarryAnError()
    {
        Assert.Throws<InvalidOperationException>(() => new InconsistentResult(true, SomeError));
    }

    [Fact]
    public void FailureCannotCarryNoError()
    {
        Assert.Throws<InvalidOperationException>(() => new InconsistentResult(false, Error.None));
    }

    [Fact]
    public void ConsistentSubclassIsAccepted()
    {
        var success = new InconsistentResult(true, Error.None);
        var failure = new InconsistentResult(false, SomeError);

        Assert.True(success.IsSuccess);
        Assert.True(failure.IsFailure);
        Assert.Equal(SomeError, failure.Error);
    }

    [Fact]
    public void TypedSuccessCarriesTheValue()
    {
        var result = Result.Success(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
        Assert.Equal(Error.None, result.Error);
    }

    [Fact]
    public void TypedFailureCarriesItsError()
    {
        var result = Result.Failure<int>(SomeError);

        Assert.True(result.IsFailure);
        Assert.Equal(SomeError, result.Error);
    }

    [Fact]
    public void TypedFailureWithoutAnErrorIsRejected()
    {
        Assert.Throws<InvalidOperationException>(() => Result.Failure<int>(Error.None));
    }

    [Fact]
    public void TypedResultIsAResult()
    {
        Result result = Result.Success("value");

        Assert.True(result.IsSuccess);
    }

    private sealed class InconsistentResult(bool isSuccess, Error error) : Result(isSuccess, error);
}
