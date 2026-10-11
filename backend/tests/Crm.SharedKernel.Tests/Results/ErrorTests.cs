using Crm.SharedKernel.Results;

namespace Crm.SharedKernel.Tests.Results;

public sealed class ErrorTests
{
    public static TheoryData<string, ErrorType> Factories => new()
    {
        { nameof(Error.Failure), ErrorType.Failure },
        { nameof(Error.NotFound), ErrorType.NotFound },
        { nameof(Error.Validation), ErrorType.Validation },
        { nameof(Error.Conflict), ErrorType.Conflict },
        { nameof(Error.Forbidden), ErrorType.Forbidden },
        { nameof(Error.Domain), ErrorType.Domain },
        { nameof(Error.Unauthorized), ErrorType.Unauthorized },
    };

    [Theory]
    [MemberData(nameof(Factories))]
    public void FactoryCreatesAnErrorOfItsType(string factory, ErrorType expected)
    {
        var error = Create(factory, "Some.Code", "Some description.");

        Assert.Equal(expected, error.Type);
        Assert.Equal("Some.Code", error.Code);
        Assert.Equal("Some description.", error.Description);
    }

    [Fact]
    public void NoneIsEmptyAndTypedAsFailure()
    {
        Assert.Equal(string.Empty, Error.None.Code);
        Assert.Equal(string.Empty, Error.None.Description);
        Assert.Equal(ErrorType.Failure, Error.None.Type);
    }

    [Fact]
    public void NoneEqualsAnEmptyFailureError()
    {
        Assert.Equal(new Error(string.Empty, string.Empty, ErrorType.Failure), Error.None);
    }

    [Fact]
    public void ErrorsWithTheSameValuesAreEqual()
    {
        Assert.Equal(Error.Conflict("A", "b"), Error.Conflict("A", "b"));
        Assert.Equal(Error.Conflict("A", "b").GetHashCode(), Error.Conflict("A", "b").GetHashCode());
    }

    [Fact]
    public void ErrorsDifferingInTypeCodeOrDescriptionAreNotEqual()
    {
        var error = Error.Conflict("A", "b");

        Assert.NotEqual(error, Error.NotFound("A", "b"));
        Assert.NotEqual(error, Error.Conflict("B", "b"));
        Assert.NotEqual(error, Error.Conflict("A", "c"));
    }

    [Fact]
    public void ErrorTypeHasTheSevenDocumentedMembers()
    {
        var names = Enum.GetNames<ErrorType>();

        Assert.Equal(7, names.Length);
        Assert.Contains(nameof(ErrorType.Unauthorized), names);
    }

    private static Error Create(string factory, string code, string description) => factory switch
    {
        nameof(Error.Failure) => Error.Failure(code, description),
        nameof(Error.NotFound) => Error.NotFound(code, description),
        nameof(Error.Validation) => Error.Validation(code, description),
        nameof(Error.Conflict) => Error.Conflict(code, description),
        nameof(Error.Forbidden) => Error.Forbidden(code, description),
        nameof(Error.Domain) => Error.Domain(code, description),
        nameof(Error.Unauthorized) => Error.Unauthorized(code, description),
        _ => throw new ArgumentOutOfRangeException(nameof(factory), factory, null),
    };
}
