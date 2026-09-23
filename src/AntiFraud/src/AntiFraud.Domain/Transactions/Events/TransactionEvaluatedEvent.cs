using AntiFraud.Domain.Common;

namespace AntiFraud.Domain.Transactions.Events;

public sealed record TransactionEvaluatedEvent(Guid TransactionId, FraudDecision Decision, string Reason) : IDomainEvent
{
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
