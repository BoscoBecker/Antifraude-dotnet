namespace AntiFraud.Application.Abstractions;

public sealed record PendingOutboxMessage(Guid Id, string Type, string Payload, int Attempts);

public interface IOutboxProcessor
{
    Task<IReadOnlyList<PendingOutboxMessage>> GetPendingAsync(int batchSize, CancellationToken cancellationToken);

    Task MarkProcessedAsync(Guid messageId, CancellationToken cancellationToken);

    Task MarkFailedAsync(Guid messageId, string error, CancellationToken cancellationToken);
}
