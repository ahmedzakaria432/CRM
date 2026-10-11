using Crm.Modules.Tickets.Application.Specifications;
using Crm.Modules.Tickets.Tests.Support;

namespace Crm.Modules.Tickets.Tests.Application.Specifications;

/// <summary>TicketQueueParser and TicketSortParser.</summary>
public sealed class ParsersTests
{
    [Theory]
    [InlineData("all", TicketQueueKind.All)]
    [InlineData("ALL", TicketQueueKind.All)]
    [InlineData("mine", TicketQueueKind.Mine)]
    [InlineData("Mine", TicketQueueKind.Mine)]
    [InlineData("unassigned", TicketQueueKind.Unassigned)]
    [InlineData("UNASSIGNED", TicketQueueKind.Unassigned)]
    public void AKnownQueueParsesInAnyCase(string value, TicketQueueKind expected)
    {
        Assert.True(TicketQueueParser.TryParse(value, out TicketQueueKind queue));
        Assert.Equal(expected, queue);
    }

    [Theory]
    [InlineData("")]
    [InlineData("inbox")]
    [InlineData("0")]
    [InlineData(" mine")]
    public void AnUnknownQueueFailsWithAll(string value)
    {
        Assert.False(TicketQueueParser.TryParse(value, out TicketQueueKind queue));
        Assert.Equal(TicketQueueKind.All, queue);
    }

    [Fact]
    public void ANullQueueIsRefused()
    {
        Func<string, bool> parse = v => TicketQueueParser.TryParse(v, out _);

        NullArguments.AssertThrowsArgumentNull(parse, (object?)null);
    }

    [Theory]
    [InlineData("createdAt", TicketSort.CreatedAt)]
    [InlineData("CREATEDAT", TicketSort.CreatedAt)]
    [InlineData("modifiedAt", TicketSort.ModifiedAt)]
    [InlineData("modifiedat", TicketSort.ModifiedAt)]
    [InlineData("priority", TicketSort.Priority)]
    [InlineData("Priority", TicketSort.Priority)]
    [InlineData("number", TicketSort.Number)]
    [InlineData("NUMBER", TicketSort.Number)]
    public void AKnownSortKeyParsesInAnyCase(string value, TicketSort expected)
    {
        Assert.True(TicketSortParser.TryParse(value, out TicketSort sort));
        Assert.Equal(expected, sort);
    }

    [Theory]
    [InlineData("")]
    [InlineData("price")]
    [InlineData("1")]
    public void AnUnknownSortKeyFailsWithCreatedAt(string value)
    {
        Assert.False(TicketSortParser.TryParse(value, out TicketSort sort));
        Assert.Equal(TicketSort.CreatedAt, sort);
    }

    [Fact]
    public void ANullSortKeyIsRefused()
    {
        Func<string, bool> parse = v => TicketSortParser.TryParse(v, out _);

        NullArguments.AssertThrowsArgumentNull(parse, (object?)null);
    }

    [Fact]
    public void TheEnumsListTheDocumentedMembersInOrder()
    {
        Assert.Equal(["All", "Mine", "Unassigned"], Enum.GetNames<TicketQueueKind>());
        Assert.Equal(["CreatedAt", "ModifiedAt", "Priority", "Number"], Enum.GetNames<TicketSort>());
    }
}
