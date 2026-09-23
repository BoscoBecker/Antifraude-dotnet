using AntiFraud.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AntiFraud.Infrastructure.Persistence.Configurations;

internal sealed class TransactionEntityConfiguration : IEntityTypeConfiguration<TransactionEntity>
{
    public void Configure(EntityTypeBuilder<TransactionEntity> builder)
    {
        builder.ToTable("transactions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(128).IsRequired();
        builder.HasIndex(x => x.IdempotencyKey)
            .IsUnique()
            .HasDatabaseName("ux_transactions_idempotency_key");

        builder.Property(x => x.ExternalReference).HasColumnName("external_reference").HasMaxLength(64).IsRequired();
        builder.HasIndex(x => x.ExternalReference)
            .HasDatabaseName("ix_transactions_external_reference");

        builder.Property(x => x.MerchantId).HasColumnName("merchant_id").HasMaxLength(64).IsRequired();
        builder.Property(x => x.CustomerId).HasColumnName("customer_id").HasMaxLength(64).IsRequired();
        builder.HasIndex(x => new { x.CustomerId, x.CreatedAtUtc })
            .HasDatabaseName("ix_transactions_customer_created");

        builder.Property(x => x.Amount).HasColumnName("amount").HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
        builder.Property(x => x.PaymentMethod).HasColumnName("payment_method").HasMaxLength(32).IsRequired();
        builder.Property(x => x.IpAddress).HasColumnName("ip_address").HasMaxLength(64);
        builder.Property(x => x.DeviceFingerprint).HasColumnName("device_fingerprint").HasMaxLength(128);
        builder.Property(x => x.Status).HasColumnName("status");
        builder.Property(x => x.Decision).HasColumnName("decision");
        builder.Property(x => x.DecisionReason).HasColumnName("decision_reason").HasMaxLength(512);
        builder.Property(x => x.ProcessingAttempts).HasColumnName("processing_attempts").HasDefaultValue(0);

        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
        builder.Property(x => x.CompletedAtUtc).HasColumnName("completed_at_utc").HasColumnType("timestamp with time zone");

        builder.HasMany(x => x.Evaluations)
            .WithOne(x => x.Transaction)
            .HasForeignKey(x => x.TransactionId)
            .HasConstraintName("fk_fraud_evaluations_transaction")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
