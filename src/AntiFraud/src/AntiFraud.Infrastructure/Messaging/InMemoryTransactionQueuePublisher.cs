using System.Threading.Channels;
using AntiFraud.Application.Abstractions;

namespace AntiFraud.Infrastructure.Messaging;

/// <summary>
/// Fallback para desenvolvimento local sem RabbitMQ.
/// </summary>
public sealed class InMemoryTransactionQueuePublisher : ITransactionQueuePublisher
{
    private readonly Channel<Guid> _channel;

    public InMemoryTransactionQueuePublisher()
    {
        _channel = Channel.CreateUnbounded<Guid>();
    }

    public ChannelReader<Guid> Reader => _channel.Reader;

    public Task PublishTransactionReceivedAsync(Guid transactionId, CancellationToken cancellationToken) =>
        _channel.Writer.WriteAsync(transactionId, cancellationToken).AsTask();
}
