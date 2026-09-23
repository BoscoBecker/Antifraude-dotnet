namespace AntiFraud.Domain.Common;

public interface IDomainEvent
{
    DateTime OccurredOnUtc { get; }
}
