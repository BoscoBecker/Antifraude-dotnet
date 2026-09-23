namespace AntiFraud.Application.Abstractions;

public interface ITransactionQueuePublisher
{
    Task PublishTransactionReceivedAsync(Guid transactionId, CancellationToken cancellationToken);
}
