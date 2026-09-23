namespace AntiFraud.Infrastructure.Persistence.Entities;

public sealed class TransactionEntity
{
    public Guid Id { get; set; }
    public string IdempotencyKey { get; set; } = null!;
    public string ExternalReference { get; set; } = null!;
    public string MerchantId { get; set; } = null!;
    public string CustomerId { get; set; } = null!;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = null!;
    public string PaymentMethod { get; set; } = null!;
    public string? IpAddress { get; set; }
    public string? DeviceFingerprint { get; set; }
    public int Status { get; set; }
    public int Decision { get; set; }
    public string? DecisionReason { get; set; }
    public int ProcessingAttempts { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }

    public ICollection<FraudEvaluationEntity> Evaluations { get; set; } = [];
}
