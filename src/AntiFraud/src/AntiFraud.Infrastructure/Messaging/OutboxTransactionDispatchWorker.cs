using System.Text.Json;
using AntiFraud.Application.Abstractions;
using AntiFraud.Application.Transactions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AntiFraud.Infrastructure.Messaging;

/// <summary>
/// Processa outbox no Worker (funciona com API e Worker em processos separados).
/// </summary>
public sealed class OutboxTransactionDispatchWorker(
    IServiceProvider serviceProvider,
    ILogger<OutboxTransactionDispatchWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private const int BatchSize = 20;

    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ILogger<OutboxTransactionDispatchWorker> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outbox dispatch worker started.");

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
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Outbox dispatch loop error");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task ProcessMessageAsync(PendingOutboxMessage message, CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var outbox = scope.ServiceProvider.GetRequiredService<IOutboxProcessor>();

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

            var evaluationService = scope.ServiceProvider.GetRequiredService<IFraudEvaluationService>();
            await evaluationService.ProcessAsync(transactionId, stoppingToken);
            await outbox.MarkProcessedAsync(message.Id, stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Outbox message {MessageId} failed", message.Id);
            await outbox.MarkFailedAsync(message.Id, ex.Message, stoppingToken);
        }
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
