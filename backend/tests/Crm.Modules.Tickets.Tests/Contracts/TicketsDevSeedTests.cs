using System.Reflection;
using Crm.Modules.Tickets.Contracts.DevSeed;

namespace Crm.Modules.Tickets.Tests.Contracts;

public sealed class TicketsDevSeedTests
{
    private static (string Name, Guid Value)[] Ids() =>
    [
        .. typeof(TicketsDevSeed).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(Guid))
            .Select(f => (f.Name, f.GetValue(null) is Guid g ? g : Guid.Empty)),
    ];

    [Fact]
    public void ThereAreTwentyTwoIds() => Assert.Equal(22, Ids().Length);

    [Fact]
    public void EveryIdIsNonEmptyAndUnique()
    {
        (string Name, Guid Value)[] ids = Ids();

        Assert.DoesNotContain(ids, i => i.Value == Guid.Empty);
        Assert.Equal(ids.Length, ids.Select(i => i.Value).Distinct().Count());
    }

    [Fact]
    public void CategoryIdsUseTheCategoryPatternAndTicketIdsTheTicketPattern()
    {
        (string Name, Guid Value)[] ids = Ids();

        Assert.Equal(7, ids.Count(i => i.Name.EndsWith("CategoryId", StringComparison.Ordinal)));
        Assert.All(ids.Where(i => i.Name.EndsWith("CategoryId", StringComparison.Ordinal)), i => Assert.Contains("-0001-", i.Value.ToString(), StringComparison.Ordinal));
        Assert.Equal(15, ids.Count(i => i.Name.EndsWith("TicketId", StringComparison.Ordinal)));
        Assert.All(ids.Where(i => i.Name.EndsWith("TicketId", StringComparison.Ordinal)), i => Assert.Contains("-0000-", i.Value.ToString(), StringComparison.Ordinal));
    }

    [Fact]
    public void TheTicketIdsAreNumberedOneToFifteen()
    {
        Guid[] tickets =
        [
            TicketsDevSeed.AhmedNewTicketId, TicketsDevSeed.NajdOpenTicketId, TicketsDevSeed.RedSeaNewTicketId, TicketsDevSeed.AlNoorOpenTicketId,
            TicketsDevSeed.AlNoorPendingTicketId, TicketsDevSeed.AlNoorYousefNewTicketId, TicketsDevSeed.AlNoorYousefResolvedTicketId,
            TicketsDevSeed.AlNoorClosedTicketId, TicketsDevSeed.AlNoorOnHoldTicketId, TicketsDevSeed.AlNoorCancelledTicketId,
            TicketsDevSeed.GulfRetailNewTicketId, TicketsDevSeed.GulfRetailOpenTicketId, TicketsDevSeed.RedSeaOpenTicketId,
            TicketsDevSeed.RedSeaResolvedTicketId, TicketsDevSeed.RedSeaHighNewTicketId,
        ];

        for (int number = 1; number <= tickets.Length; number++)
        {
            Assert.EndsWith(number.ToString("D12", System.Globalization.CultureInfo.InvariantCulture), tickets[number - 1].ToString(), StringComparison.Ordinal);
        }
    }
}
