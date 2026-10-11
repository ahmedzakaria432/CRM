using Crm.BuildingBlocks.Application.Exceptions;

namespace Crm.BuildingBlocks.Tests.Application.Exceptions;

public sealed class ApplicationExceptionsTests
{
    [Fact]
    public void ConcurrencyConflictKeepsTheInnerExceptionAndAFixedMessage()
    {
        var inner = new InvalidOperationException("ef said no");

        var exception = new ConcurrencyConflictException(inner);

        Assert.Same(inner, exception.InnerException);
        Assert.Equal("The record was modified by someone else.", exception.Message);
    }

    [Fact]
    public void ScopeViolationExposesItsEntityTypeAndOperation()
    {
        var exception = new OrganisationScopeViolationException("Ticket", "Write");

        Assert.Equal("Ticket", exception.EntityType);
        Assert.Equal("Write", exception.Operation);
    }

    [Fact]
    public void ScopeViolationMessageNamesBothAndTheScope()
    {
        var exception = new OrganisationScopeViolationException("Ticket", "Read");

        Assert.Contains("Read", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Ticket", exception.Message, StringComparison.Ordinal);
        Assert.Contains("organisation scope", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BothAreSealedExceptions()
    {
        Assert.True(typeof(ConcurrencyConflictException).IsSealed);
        Assert.True(typeof(OrganisationScopeViolationException).IsSealed);
        Assert.True(typeof(ConcurrencyConflictException).IsSubclassOf(typeof(Exception)));
        Assert.True(typeof(OrganisationScopeViolationException).IsSubclassOf(typeof(Exception)));
    }
}
