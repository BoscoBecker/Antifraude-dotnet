using AntiFraud.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AntiFraud.Infrastructure.Persistence.Configurations;

internal sealed class AuditLogEntityConfiguration : IEntityTypeConfiguration<AuditLogEntity>
{
    public void Configure(EntityTypeBuilder<AuditLogEntity> builder)
    {
        builder.ToTable("audit_logs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TransactionId).HasColumnName("transaction_id");
        builder.Property(x => x.Action).HasColumnName("action").HasMaxLength(64).IsRequired();
        builder.Property(x => x.Payload).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");

        builder.HasIndex(x => new { x.TransactionId, x.CreatedAtUtc })
            .HasDatabaseName("ix_audit_logs_transaction_created");

        builder.HasOne<TransactionEntity>()
            .WithMany()
            .HasForeignKey(x => x.TransactionId)
            .HasConstraintName("fk_audit_logs_transaction")
            .OnDelete(DeleteBehavior.SetNull);
    }
}
