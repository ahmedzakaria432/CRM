using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.BuildingBlocks.Infrastructure.Outbox;

// No schema here: ModuleDbContext's HasDefaultSchema puts both tables in the owning module's schema.

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("outbox_messages");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.Type).HasMaxLength(500).IsRequired();
        builder.Property(m => m.Content).IsRequired();
        builder.Property(m => m.CorrelationId).HasMaxLength(64);
        builder.Property(m => m.TraceParent).HasMaxLength(55);
        builder.Property(m => m.LastError).HasMaxLength(OutboxMessage.MaxErrorLength);

        // The dispatcher's poll: undelivered rows only, so the index stays small however much history accrues.
        builder.HasIndex(m => new { m.ProcessedAtUtc, m.NextAttemptAtUtc })
            .HasFilter("[ProcessedAtUtc] IS NULL");
    }
}

public sealed class OutboxMessageConsumerConfiguration : IEntityTypeConfiguration<OutboxMessageConsumer>
{
    public void Configure(EntityTypeBuilder<OutboxMessageConsumer> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("outbox_message_consumers");

        // The composite key IS the idempotency guarantee: two deliveries of one message to one handler cannot
        // both commit.
        builder.HasKey(c => new { c.MessageId, c.Handler });
        builder.Property(c => c.Handler).HasMaxLength(300);
    }
}
