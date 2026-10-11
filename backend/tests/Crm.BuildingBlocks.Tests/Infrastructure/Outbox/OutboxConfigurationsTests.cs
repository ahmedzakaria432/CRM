using Crm.BuildingBlocks.Infrastructure.Outbox;
using Crm.BuildingBlocks.Tests.Support.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Crm.BuildingBlocks.Tests.Infrastructure.Outbox;

public sealed class OutboxConfigurationsTests
{
    private static async Task<(IEntityType Messages, IEntityType Consumers)> ModelAsync()
    {
        await using var context = TestDbContext.Store(TestDbContext.NewDatabaseName());
        var model = context.GetService<IDesignTimeModel>().Model;
        return (
            Assert.IsAssignableFrom<IEntityType>(model.FindEntityType(typeof(OutboxMessage))),
            Assert.IsAssignableFrom<IEntityType>(model.FindEntityType(typeof(OutboxMessageConsumer))));
    }

    [Fact]
    public async Task TheTablesAreNamedOutboxMessagesAndOutboxMessageConsumers()
    {
        var (messages, consumers) = await ModelAsync();

        Assert.Equal("outbox_messages", messages.GetTableName());
        Assert.Equal("outbox_message_consumers", consumers.GetTableName());
    }

    [Fact]
    public async Task TheMessageColumnsHaveTheirLengthsAndRequiredness()
    {
        var (messages, _) = await ModelAsync();

        var type = messages.FindProperty(nameof(OutboxMessage.Type));
        Assert.Equal(500, type?.GetMaxLength());
        Assert.False(type?.IsNullable);
        Assert.False(messages.FindProperty(nameof(OutboxMessage.Content))?.IsNullable);
        Assert.Equal(64, messages.FindProperty(nameof(OutboxMessage.CorrelationId))?.GetMaxLength());
        Assert.Equal(55, messages.FindProperty(nameof(OutboxMessage.TraceParent))?.GetMaxLength());
        Assert.Equal(2000, messages.FindProperty(nameof(OutboxMessage.LastError))?.GetMaxLength());
    }

    [Fact]
    public async Task TheMessageIdIsNeverGeneratedByTheStore()
    {
        var (messages, _) = await ModelAsync();

        var key = Assert.IsAssignableFrom<IKey>(messages.FindPrimaryKey());
        Assert.Equal([nameof(OutboxMessage.Id)], key.Properties.Select(p => p.Name));
        Assert.Equal(ValueGenerated.Never, messages.FindProperty(nameof(OutboxMessage.Id))?.ValueGenerated);
    }

    [Fact]
    public async Task TheConsumerKeyIsTheMessageIdAndHandlerPair()
    {
        var (_, consumers) = await ModelAsync();

        var key = Assert.IsAssignableFrom<IKey>(consumers.FindPrimaryKey());

        Assert.Equal([nameof(OutboxMessageConsumer.MessageId), nameof(OutboxMessageConsumer.Handler)], key.Properties.Select(p => p.Name));
        Assert.Equal(300, consumers.FindProperty(nameof(OutboxMessageConsumer.Handler))?.GetMaxLength());
    }

    [Fact]
    public async Task ThePollIndexCoversProcessedAndNextAttemptColumns()
    {
        var (messages, _) = await ModelAsync();

        var index = Assert.Single(messages.GetIndexes());

        Assert.Equal([nameof(OutboxMessage.ProcessedAtUtc), nameof(OutboxMessage.NextAttemptAtUtc)], index.Properties.Select(p => p.Name));
        Assert.Equal("[ProcessedAtUtc] IS NULL", index.GetFilter());
    }
}
