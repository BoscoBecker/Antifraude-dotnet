using System.Text.Json;
using AntiFraud.Application.Abstractions;
using AntiFraud.Infrastructure.Persistence;
using AntiFraud.Infrastructure.Persistence.Entities;

namespace AntiFraud.Infrastructure.Audit;

public sealed class EfAuditLogger(AntiFraudDbContext context) : IAuditLogger
{
    private readonly AntiFraudDbContext _context = context;

    public async Task LogAsync(string action, Guid? transactionId, string payload, CancellationToken cancellationToken)
    {
        var jsonPayload = JsonSerializer.Serialize(new { detail = payload, loggedAtUtc = DateTime.UtcNow });

        await _context.AuditLogs.AddAsync(new AuditLogEntity
        {
            Id = Guid.NewGuid(),
            Action = action,
            TransactionId = transactionId,
            Payload = jsonPayload,
            CreatedAtUtc = DateTime.UtcNow
        }, cancellationToken);
    }
}
