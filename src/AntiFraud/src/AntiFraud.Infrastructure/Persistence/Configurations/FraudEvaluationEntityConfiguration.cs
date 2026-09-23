using AntiFraud.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AntiFraud.Infrastructure.Persistence.Configurations;

internal sealed class FraudEvaluationEntityConfiguration : IEntityTypeConfiguration<FraudEvaluationEntity>
{
    public void Configure(EntityTypeBuilder<FraudEvaluationEntity> builder)
    {
        builder.ToTable("fraud_evaluations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TransactionId).HasColumnName("transaction_id");
        builder.Property(x => x.RuleCode).HasColumnName("rule_code").HasMaxLength(64).IsRequired();
        builder.Property(x => x.Passed).HasColumnName("passed");
        builder.Property(x => x.Score).HasColumnName("score");
        builder.Property(x => x.Message).HasColumnName("message").HasMaxLength(512);
        builder.Property(x => x.EvaluatedAtUtc).HasColumnName("evaluated_at_utc").HasColumnType("timestamp with time zone");
        builder.HasIndex(x => x.TransactionId)
            .HasDatabaseName("ix_fraud_evaluations_transaction");
    }
}
