using AntiFraud.Domain.Repositories;
using AntiFraud.Domain.Transactions;
using AntiFraud.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AntiFraud.Infrastructure.Persistence;

public sealed class TransactionRepository(AntiFraudDbContext context) : ITransactionRepository
{
    private readonly AntiFraudDbContext _context = context;

    public async Task<Transaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await _context.Transactions
            .AsNoTracking()
            .Include(x => x.Evaluations)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return entity is null ? null : TransactionMapper.ToDomain(entity);
    }


    public async Task<Transaction?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken)
    {
        var entity = await _context.Transactions
            .AsNoTracking()
            .Include(x => x.Evaluations)
            .FirstOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, cancellationToken);
        return entity is null ? null : TransactionMapper.ToDomain(entity);
    }

    public async Task AddAsync(Transaction transaction, CancellationToken cancellationToken) =>     
        await _context.Transactions.AddAsync(TransactionMapper.ToEntity(transaction), cancellationToken);

    public async Task UpdateAsync(Transaction transaction, CancellationToken cancellationToken)
    {
        var affected = await _context.Transactions
            .Where(t => t.Id == transaction.Id)
            .ExecuteUpdateAsync(s => s
                    .SetProperty(t => t.Status, (int)transaction.Status)
                    .SetProperty(t => t.Decision, (int)transaction.Decision)
                    .SetProperty(t => t.DecisionReason, transaction.DecisionReason)
                    .SetProperty(t => t.ProcessingAttempts, transaction.ProcessingAttempts)
                    .SetProperty(t => t.CompletedAtUtc, transaction.CompletedAtUtc),
             cancellationToken);


        if (affected == 0)
            throw new InvalidOperationException($"Transaction {transaction.Id} not found.");

        var existingEvaluationIds = await _context.FraudEvaluations
            .AsNoTracking()
            .Where(e => e.TransactionId == transaction.Id)
            .Select(e => e.Id)
            .ToListAsync(cancellationToken);


        foreach (var evaluation in transaction.Evaluations)
        {
            if (existingEvaluationIds.Contains(evaluation.Id))
                continue;            

            await _context.FraudEvaluations.AddAsync(
                new FraudEvaluationEntity
                {
                    Id = evaluation.Id,
                    TransactionId = transaction.Id,
                    RuleCode = evaluation.RuleCode,
                    Passed = evaluation.Passed,
                    Score = evaluation.Score,
                    Message = evaluation.Message,
                    EvaluatedAtUtc = evaluation.EvaluatedAtUtc
                },
            cancellationToken);
        }
    }

    public Task<int> CountRecentByCustomerAsync(string customerId, TimeSpan window, CancellationToken cancellationToken)
    {
        var since = DateTime.UtcNow.Subtract(window);
        return _context.Transactions.AsNoTracking()
            .CountAsync(x => x.CustomerId == customerId && x.CreatedAtUtc >= since, cancellationToken);
    }
}


