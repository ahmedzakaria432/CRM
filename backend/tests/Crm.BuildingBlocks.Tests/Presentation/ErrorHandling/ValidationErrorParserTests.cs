using Crm.BuildingBlocks.Presentation.ErrorHandling;

namespace Crm.BuildingBlocks.Tests.Presentation.ErrorHandling;

public sealed class ValidationErrorParserTests
{
    [Fact]
    public void EntriesBecomeFieldKeys()
    {
        var errors = ValidationErrorParser.Parse("Name: Name is required. | Email: Email is invalid.");

        Assert.Equal(2, errors.Count);
        Assert.Equal(["Name is required."], errors["Name"]);
        Assert.Equal(["Email is invalid."], errors["Email"]);
    }

    [Fact]
    public void ARepeatedFieldGroupsItsMessagesInOrder()
    {
        var errors = ValidationErrorParser.Parse("Name: first | Email: other | Name: second");

        Assert.Equal(["first", "second"], errors["Name"]);
        Assert.Equal(["other"], errors["Email"]);
    }

    [Fact]
    public void AnEntryWithoutAColonUsesTheWholeEntryAsFieldAndMessage()
    {
        var errors = ValidationErrorParser.Parse("something went wrong");

        Assert.Equal(["something went wrong"], errors["something went wrong"]);
    }

    [Fact]
    public void OnlyTheFirstColonSplitsTheEntry()
    {
        var errors = ValidationErrorParser.Parse("Name: x: y");

        Assert.Equal(["x: y"], errors["Name"]);
    }

    [Fact]
    public void WhitespaceIsTrimmed()
    {
        var errors = ValidationErrorParser.Parse("  Name  :   padded message   ");

        Assert.Equal(["padded message"], errors["Name"]);
    }

    [Fact]
    public void AnEmptyDescriptionGivesNoErrors()
    {
        Assert.Empty(ValidationErrorParser.Parse(string.Empty));
    }

    [Fact]
    public void ConsecutiveSeparatorsAreIgnored()
    {
        var errors = ValidationErrorParser.Parse("Name: a |  | Email: b");

        Assert.Equal(["Email", "Name"], errors.Keys.Order());
    }

    [Fact]
    public void AMessageThatContainsTheSeparatorIsMisattributedButNeverThrows()
    {
        // Documented limitation in the parser's summary: " | " inside a message splits it.
        var errors = ValidationErrorParser.Parse("Name: a | b");

        Assert.Equal(["a"], errors["Name"]);
        Assert.Equal(["b"], errors["b"]);
    }
}
