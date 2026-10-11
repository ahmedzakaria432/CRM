using Crm.Modules.Tickets.Domain;

namespace Crm.Modules.Tickets.Tests.Domain;

public sealed class IdTests
{
    [Fact]
    public void ATicketIdIsNewAndNonEmpty()
    {
        TicketId first = TicketId.New();
        TicketId second = TicketId.New();

        Assert.NotEqual(Guid.Empty, first.Value);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void ACategoryIdIsNewAndNonEmpty()
    {
        CategoryId first = CategoryId.New();
        CategoryId second = CategoryId.New();

        Assert.NotEqual(Guid.Empty, first.Value);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void IdsWithTheSameValueAreEqual()
    {
        var value = new Guid("11111111-1111-1111-1111-111111111111");

        Assert.Equal(new TicketId(value), new TicketId(value));
        Assert.Equal(new CategoryId(value), new CategoryId(value));
        Assert.Equal(new TicketId(value).GetHashCode(), new TicketId(value).GetHashCode());
    }
}
