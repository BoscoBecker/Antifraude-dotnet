using System.Text;
using System.Text.Json;
using AntiFraud.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace AntiFraud.Infrastructure.Messaging;

public sealed class RabbitMqTransactionQueuePublisher(
    IOptions<RabbitMqOptions> options,
    ILogger<RabbitMqTransactionQueuePublisher> logger) : ITransactionQueuePublisher, IAsyncDisposable, IDisposable
{
    private readonly RabbitMqOptions _options = options.Value;
    private readonly ILogger<RabbitMqTransactionQueuePublisher> _logger = logger;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private IConnection? _connection;
    private IChannel? _publishChannel;

    public async Task PublishTransactionReceivedAsync(Guid transactionId, CancellationToken cancellationToken)
    {
        await EnsurePublishChannelAsync(cancellationToken);

        var payload = JsonSerializer.Serialize(new { transactionId, occurredAtUtc = DateTime.UtcNow });
        var body = Encoding.UTF8.GetBytes(payload);
        var properties = new BasicProperties
        {
            Persistent = true,
            MessageId = transactionId.ToString(),
            ContentType = "application/json"
        };

        try
        {
            await _publishChannel!.BasicPublishAsync(
                _options.ExchangeName,
                _options.RoutingKey,
                mandatory: false,
                basicProperties: properties,
                body: new ReadOnlyMemory<byte>(body),
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish transaction {TransactionId}; outbox worker can retry", transactionId);
            throw;
        }
    }

    private async Task EnsurePublishChannelAsync(CancellationToken cancellationToken)
    {
        if (_publishChannel is { IsOpen: true })
        {
            return;
        }

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_publishChannel is { IsOpen: true })
            {
                return;
            }

            var factory = CreateFactory();
            _connection = await factory.CreateConnectionAsync(cancellationToken);
            _publishChannel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

            await _publishChannel.ExchangeDeclareAsync(
                _options.ExchangeName,
                ExchangeType.Topic,
                durable: true,
                cancellationToken: cancellationToken);

            await _publishChannel.QueueDeclareAsync(
                _options.QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                cancellationToken: cancellationToken);

            await _publishChannel.QueueBindAsync(
                _options.QueueName,
                _options.ExchangeName,
                _options.RoutingKey,
                cancellationToken: cancellationToken);
        }
        finally
        {
            _initLock.Release();
        }
    }

    private ConnectionFactory CreateFactory() =>
        new()
        {
            HostName = _options.HostName,
            Port = _options.Port,
            UserName = _options.UserName,
            Password = _options.Password
        };

    public async ValueTask DisposeAsync()
    {
        if (_publishChannel is not null)
            await _publishChannel.DisposeAsync();
        if (_connection is not null)
            await _connection.DisposeAsync();

        _initLock.Dispose();
    }

    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();
}
