using AntiFraud.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace AntiFraud.Infrastructure.Persistence;

public sealed class OutboxProcessor : IOutboxProcessor
{
    private readonly AntiFraudDbContext _context;

    public OutboxProcessor(AntiFraudDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<PendingOutboxMessage>> GetPendingAsync(int batchSize, CancellationToken cancellationToken)
    {
        var rows = await _context.OutboxMessages
            .AsNoTracking()
            .Where(x => x.ProcessedAtUtc == null)
            .OrderBy(x => x.CreatedAtUtc)
            .Take(batchSize)
            .Select(x => new PendingOutboxMessage(x.Id, x.Type, x.Payload, x.Attempts))
            .ToListAsync(cancellationToken);

        return rows;
    }

    public Task MarkProcessedAsync(Guid messageId, CancellationToken cancellationToken) =>
        _context.OutboxMessages
            .Where(x => x.Id == messageId && x.ProcessedAtUtc == null)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(x => x.ProcessedAtUtc, DateTime.UtcNow)
                    .SetProperty(x => x.LastError, (string?)null),
                cancellationToken);

    public Task MarkFailedAsync(Guid messageId, string error, CancellationToken cancellationToken)
    {
        var lastError = error.Length > 2000 ? error[..2000] : error;

        return _context.OutboxMessages
            .Where(x => x.Id == messageId && x.ProcessedAtUtc == null)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(x => x.Attempts, x => x.Attempts + 1)
                    .SetProperty(x => x.LastError, lastError),
                cancellationToken);
    }
}
