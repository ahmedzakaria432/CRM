namespace Crm.SharedKernel.Tests;

public sealed class DomainExceptionTests
{
    [Fact]
    public void MessageIsTheErrorMessage()
    {
        var exception = new DomainException(new DomainError("Ticket.Closed", "The ticket is closed."));

        Assert.Equal("The ticket is closed.", exception.Message);
    }

    [Fact]
    public void ErrorIsTheInstancePassedIn()
    {
        var error = new DomainError("Ticket.Closed", "The ticket is closed.");

        var exception = new DomainException(error);

        Assert.Same(error, exception.Error);
        Assert.Equal("Ticket.Closed", exception.Error.Code);
    }

    [Fact]
    public void DomainErrorHasValueEquality()
    {
        Assert.Equal(new DomainError("A", "b"), new DomainError("A", "b"));
        Assert.NotEqual(new DomainError("A", "b"), new DomainError("A", "c"));
        Assert.NotEqual(new DomainError("A", "b"), new DomainError("B", "b"));
    }

    [Fact]
    public void ItIsSealedAndDerivesFromException()
    {
        Assert.True(typeof(DomainException).IsSealed);
        Assert.True(typeof(DomainException).IsSubclassOf(typeof(Exception)));
    }

    [Fact]
    public void ItCanBeCaughtAsADomainException()
    {
        var thrown = Assert.Throws<DomainException>(() => ThrowDomainException());

        Assert.Equal("X", thrown.Error.Code);
    }

    private static void ThrowDomainException() => throw new DomainException(new DomainError("X", "y"));
}
