using AntiFraud.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AntiFraud.Infrastructure.Persistence.Configurations;

internal sealed class OutboxMessageEntityConfiguration : IEntityTypeConfiguration<OutboxMessageEntity>
{
    public void Configure(EntityTypeBuilder<OutboxMessageEntity> builder)
    {
        builder.ToTable("outbox_messages");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Type).HasColumnName("type").HasMaxLength(128).IsRequired();
        builder.Property(x => x.Payload).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
        builder.Property(x => x.ProcessedAtUtc).HasColumnName("processed_at_utc").HasColumnType("timestamp with time zone");
        builder.Property(x => x.Attempts).HasColumnName("attempts").HasDefaultValue(0);
        builder.Property(x => x.LastError).HasColumnName("last_error").HasColumnType("text");

        builder.HasIndex(x => x.CreatedAtUtc)
            .HasDatabaseName("ix_outbox_messages_pending")
            .HasFilter("processed_at_utc IS NULL");
    }
}
