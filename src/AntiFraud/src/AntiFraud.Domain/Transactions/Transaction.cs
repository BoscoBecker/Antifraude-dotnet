using AntiFraud.Domain.Common;
using AntiFraud.Domain.Transactions.Events;

namespace AntiFraud.Domain.Transactions;

public sealed class Transaction : AggregateRoot
{
    public IdempotencyKey IdempotencyKey { get; private set; } = null!;
    public string ExternalReference { get; private set; } = null!;
    public string MerchantId { get; private set; } = null!;
    public string CustomerId { get; private set; } = null!;
    public Money Amount { get; private set; } = null!;
    public string PaymentMethod { get; private set; } = null!;
    public string? IpAddress { get; private set; }
    public string? DeviceFingerprint { get; private set; }
    public TransactionStatus Status { get; private set; }
    public FraudDecision Decision { get; private set; }
    public string? DecisionReason { get; private set; }
    public int ProcessingAttempts { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }

    private readonly List<FraudEvaluationEntry> _evaluations = [];
    public IReadOnlyCollection<FraudEvaluationEntry> Evaluations => _evaluations.AsReadOnly();

    private Transaction()
    {
    }

    public static Transaction Receive(
        IdempotencyKey idempotencyKey,
        string externalReference,
        string merchantId,
        string customerId,
        Money amount,
        string paymentMethod,
        string? ipAddress,
        string? deviceFingerprint)
    {
        if (string.IsNullOrWhiteSpace(externalReference))
        {
            throw new ArgumentException("External reference is required.", nameof(externalReference));
        }

        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            IdempotencyKey = idempotencyKey,
            ExternalReference = externalReference.Trim(),
            MerchantId = merchantId.Trim(),
            CustomerId = customerId.Trim(),
            Amount = amount,
            PaymentMethod = paymentMethod.Trim(),
            IpAddress = ipAddress?.Trim(),
            DeviceFingerprint = deviceFingerprint?.Trim(),
            Status = TransactionStatus.Received,
            Decision = FraudDecision.Pending,
            CreatedAtUtc = DateTime.UtcNow
        };

        transaction.Raise(new TransactionReceivedEvent(transaction.Id, idempotencyKey.Value));
        return transaction;
    }

    public static Transaction Rehydrate(
        Guid id,
        IdempotencyKey idempotencyKey,
        string externalReference,
        string merchantId,
        string customerId,
        Money amount,
        string paymentMethod,
        string? ipAddress,
        string? deviceFingerprint,
        TransactionStatus status,
        FraudDecision decision,
        string? decisionReason,
        int processingAttempts,
        DateTime createdAtUtc,
        DateTime? completedAtUtc,
        IEnumerable<FraudEvaluationEntry> evaluations)
    {
        var transaction = new Transaction
        {
            Id = id,
            IdempotencyKey = idempotencyKey,
            ExternalReference = externalReference,
            MerchantId = merchantId,
            CustomerId = customerId,
            Amount = amount,
            PaymentMethod = paymentMethod,
            IpAddress = ipAddress,
            DeviceFingerprint = deviceFingerprint,
            Status = status,
            Decision = decision,
            DecisionReason = decisionReason,
            ProcessingAttempts = processingAttempts,
            CreatedAtUtc = createdAtUtc,
            CompletedAtUtc = completedAtUtc
        };

        transaction._evaluations.AddRange(evaluations);
        return transaction;
    }

    public void MarkQueued()
    {
        EnsureStatus(TransactionStatus.Received);
        Status = TransactionStatus.Queued;
    }

    public void BeginProcessing()
    {
        if (Status is not (TransactionStatus.Queued or TransactionStatus.Received))
        {
            throw new InvalidOperationException($"Cannot begin processing from status {Status}.");
        }

        Status = TransactionStatus.Processing;
        ProcessingAttempts++;
    }

    public void CompleteEvaluation(FraudDecision decision, string reason, IReadOnlyList<RuleEvaluationResult> ruleResults)
    {
        if (Status != TransactionStatus.Processing)
        {
            throw new InvalidOperationException("Transaction must be processing to complete evaluation.");
        }

        Decision = decision;
        DecisionReason = reason;
        Status = TransactionStatus.Completed;
        CompletedAtUtc = DateTime.UtcNow;

        foreach (var result in ruleResults)
        {
            _evaluations.Add(FraudEvaluationEntry.Create(result.RuleCode, result.Passed, result.Score, result.Message));
        }

        Raise(new TransactionEvaluatedEvent(Id, decision, reason));
    }

    public void MarkFailed(string reason)
    {
        DecisionReason = reason;
        Status = TransactionStatus.Failed;
        CompletedAtUtc = DateTime.UtcNow;
    }

    private void EnsureStatus(TransactionStatus expected)
    {
        if (Status != expected)
        {
            throw new InvalidOperationException($"Expected status {expected}, current {Status}.");
        }
    }
}
