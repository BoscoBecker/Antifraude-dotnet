using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using AntiFraud.Application.Transactions;
using AntiFraud.Domain.Transactions;
using AntiFraud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace AntiFraud.Infrastructure.Messaging;

public sealed class RabbitMqTransactionEvaluationConsumer(
    IServiceProvider serviceProvider,
    IOptions<RabbitMqOptions> options,
    ILogger<RabbitMqTransactionEvaluationConsumer> logger) : BackgroundService
{
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> TransactionLocks = new();

    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly RabbitMqOptions _options = options.Value;
    private readonly ILogger<RabbitMqTransactionEvaluationConsumer> _logger = logger;
    private IConnection? _connection;
    private IChannel? _channel;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var factory = new ConnectionFactory
            {
                HostName = _options.HostName,
                Port = _options.Port,
                UserName = _options.UserName,
                Password = _options.Password
            };
            _connection = await factory.CreateConnectionAsync(stoppingToken);
            _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

            await _channel.ExchangeDeclareAsync(
                _options.ExchangeName,
                ExchangeType.Topic,
                durable: true,
                cancellationToken: stoppingToken);

            await _channel.QueueDeclareAsync(
                _options.QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                cancellationToken: stoppingToken);

            await _channel.QueueBindAsync(
                _options.QueueName,
                _options.ExchangeName,
                _options.RoutingKey,
                cancellationToken: stoppingToken);

            await _channel.BasicQosAsync(0, 1, false, stoppingToken);

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += async (_, args) =>
            {
                await HandleMessageAsync(args, stoppingToken);
            };

            await _channel.BasicConsumeAsync(_options.QueueName, autoAck: false, consumer, stoppingToken);
            _logger.LogInformation("RabbitMQ consumer started on queue {Queue}", _options.QueueName);

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RabbitMQ consumer failed to start");
            throw;
        }
    }

    private async Task HandleMessageAsync(BasicDeliverEventArgs args, CancellationToken stoppingToken)
    {
        var transactionId = ParseTransactionId(args.Body);
        if (transactionId == Guid.Empty)
        {
            await _channel!.BasicNackAsync(args.DeliveryTag, false, false, stoppingToken);
            return;
        }

        var gate = TransactionLocks.GetOrAdd(transactionId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(stoppingToken);
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var evaluationService = scope.ServiceProvider.GetRequiredService<IFraudEvaluationService>();
            await evaluationService.ProcessAsync(transactionId, CancellationToken.None);
            await _channel!.BasicAckAsync(args.DeliveryTag, false, stoppingToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            if (await IsTransactionCompletedAsync(transactionId, stoppingToken))
            {
                _logger.LogInformation(
                    ex,
                    "Duplicate RabbitMQ delivery for transaction {TransactionId}; already completed. Acking message.",
                    transactionId);
                await _channel!.BasicAckAsync(args.DeliveryTag, false, stoppingToken);
                return;
            }

            _logger.LogError(ex, "Concurrency conflict processing transaction {TransactionId}", transactionId);
            await _channel!.BasicNackAsync(args.DeliveryTag, false, false, stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process RabbitMQ message for transaction {TransactionId}", transactionId);
            await _channel!.BasicNackAsync(args.DeliveryTag, false, false, stoppingToken);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<bool> IsTransactionCompletedAsync(Guid transactionId, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AntiFraudDbContext>();
        var completed = (int)TransactionStatus.Completed;
        return await db.Transactions.AsNoTracking()
            .AnyAsync(t => t.Id == transactionId && t.Status == completed, cancellationToken);
    }

    private static Guid ParseTransactionId(ReadOnlyMemory<byte> body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("transactionId", out var idProp) &&
                idProp.TryGetGuid(out var id))
            {
                return id;
            }
        }
        catch (JsonException)
        {
            // ignore
        }

        var text = Encoding.UTF8.GetString(body.Span);
        return Guid.TryParse(text, out var parsed) ? parsed : Guid.Empty;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel is not null)
            await _channel.DisposeAsync();
        if (_connection is not null)
            await _connection.DisposeAsync();

        await base.StopAsync(cancellationToken);
    }
}
