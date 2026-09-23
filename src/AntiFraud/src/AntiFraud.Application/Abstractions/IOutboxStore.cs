namespace AntiFraud.Application.Abstractions;

public interface IOutboxStore
{
    Task EnqueueTransactionReceivedAsync(Guid transactionId, CancellationToken cancellationToken);
}
