using System.Text.Json;
using AntiFraud.Application.Abstractions;
using AntiFraud.Infrastructure.Persistence.Entities;

namespace AntiFraud.Infrastructure.Persistence;

public sealed class OutboxStore(AntiFraudDbContext context) : IOutboxStore
{
    private readonly AntiFraudDbContext _context = context;

    public Task EnqueueTransactionReceivedAsync(Guid transactionId, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new { transactionId, occurredAtUtc = DateTime.UtcNow });
        return _context.OutboxMessages.AddAsync(new OutboxMessageEntity
        {
            Id = Guid.NewGuid(),
            Type = "TransactionReceived",
            Payload = payload,
            CreatedAtUtc = DateTime.UtcNow
        }, cancellationToken).AsTask();
    }
}
