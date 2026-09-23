using AntiFraud.Domain.Common;

namespace AntiFraud.Domain.Transactions.Events;

public sealed record TransactionReceivedEvent(Guid TransactionId, string IdempotencyKey) : IDomainEvent
{
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
