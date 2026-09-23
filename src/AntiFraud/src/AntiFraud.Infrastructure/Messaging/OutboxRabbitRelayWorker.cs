using System.Collections.Concurrent;
using System.Text.Json;
using AntiFraud.Application.Abstractions;
using AntiFraud.Domain.Transactions;
using AntiFraud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AntiFraud.Infrastructure.Messaging;

/// <summary>
/// Publica mensagens da outbox no RabbitMQ (host da API).
/// </summary>
public sealed class OutboxRabbitRelayWorker : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan RecoveryPublishCooldown = TimeSpan.FromMinutes(2);
    private static readonly ConcurrentDictionary<Guid, DateTime> RecoveryLastPublishUtc = new();
    private const int BatchSize = 20;

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxRabbitRelayWorker> _logger;

    public OutboxRabbitRelayWorker(
        IServiceProvider serviceProvider,
        ILogger<OutboxRabbitRelayWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outbox Rabbit relay worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                IReadOnlyList<PendingOutboxMessage> pending;
                using (var listScope = _serviceProvider.CreateScope())
                {
                    var outboxReader = listScope.ServiceProvider.GetRequiredService<IOutboxProcessor>();
                    pending = await outboxReader.GetPendingAsync(BatchSize, stoppingToken);
                }

                foreach (var message in pending)
                {
                    await ProcessMessageAsync(message, stoppingToken);
                }

                await RecoverStuckQueuedTransactionsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Outbox relay loop error");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    /// <summary>
    /// Transações ainda QUEUED sem outbox pendente (ex.: outbox marcada processada quando publish falhou).
    /// </summary>
    private async Task RecoverStuckQueuedTransactionsAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AntiFraudDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<ITransactionQueuePublisher>();

        var queuedStatus = (int)TransactionStatus.Queued;
        var stuckIds = await db.Transactions.AsNoTracking()
            .Where(t => t.Status == queuedStatus)
            .OrderBy(t => t.CreatedAtUtc)
            .Select(t => t.Id)
            .Take(BatchSize)
            .ToListAsync(stoppingToken);

        foreach (var transactionId in stuckIds)
        {
            if (await HasPendingOutboxForTransactionAsync(db, transactionId, stoppingToken))
            {
                continue;
            }

            var now = DateTime.UtcNow;
            if (RecoveryLastPublishUtc.TryGetValue(transactionId, out var lastPublish) &&
                now - lastPublish < RecoveryPublishCooldown)
            {
                continue;
            }

            try
            {
                await publisher.PublishTransactionReceivedAsync(transactionId, stoppingToken);
                RecoveryLastPublishUtc[transactionId] = now;
                _logger.LogInformation(
                    "Recovery publish to RabbitMQ for stuck queued transaction {TransactionId}",
                    transactionId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Recovery publish failed for transaction {TransactionId}",
                    transactionId);
            }
        }
    }

    private async Task ProcessMessageAsync(PendingOutboxMessage message, CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var outbox = scope.ServiceProvider.GetRequiredService<IOutboxProcessor>();
        var publisher = scope.ServiceProvider.GetRequiredService<ITransactionQueuePublisher>();

        if (!string.Equals(message.Type, "TransactionReceived", StringComparison.Ordinal))
        {
            await outbox.MarkProcessedAsync(message.Id, stoppingToken);
            return;
        }

        try
        {
            var transactionId = ParseTransactionId(message.Payload);
            if (transactionId == Guid.Empty)
            {
                await outbox.MarkFailedAsync(message.Id, "Invalid outbox payload.", stoppingToken);
                return;
            }

            await publisher.PublishTransactionReceivedAsync(transactionId, stoppingToken);
            await outbox.MarkProcessedAsync(message.Id, stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Outbox relay failed for message {MessageId}", message.Id);
            await outbox.MarkFailedAsync(message.Id, ex.Message, stoppingToken);
        }
    }

    private static async Task<bool> HasPendingOutboxForTransactionAsync(
        AntiFraudDbContext db,
        Guid transactionId,
        CancellationToken cancellationToken)
    {
        var idFragment = transactionId.ToString();
        var likePattern = $"%{idFragment}%";

        var count = await db.Database
            .SqlQuery<int>(
                $"""
                 SELECT COUNT(*)::int AS "Value"
                 FROM outbox_messages
                 WHERE processed_at_utc IS NULL
                   AND type = 'TransactionReceived'
                   AND payload::text LIKE {likePattern}
                 """)
            .SingleAsync(cancellationToken);

        return count > 0;
    }

    private static Guid ParseTransactionId(string payload)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
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

        return Guid.Empty;
    }
}
