namespace AntiFraud.Infrastructure.Persistence.Entities;

public sealed class FraudEvaluationEntity
{
    public Guid Id { get; set; }
    public Guid TransactionId { get; set; }
    public string RuleCode { get; set; } = null!;
    public bool Passed { get; set; }
    public int Score { get; set; }
    public string? Message { get; set; }
    public DateTime EvaluatedAtUtc { get; set; }

    public TransactionEntity Transaction { get; set; } = null!;
}
