using AntiFraud.Domain.Transactions;
using AntiFraud.Infrastructure.Persistence.Entities;

namespace AntiFraud.Infrastructure.Persistence;

internal static class TransactionMapper
{
    public static Transaction ToDomain(TransactionEntity entity)
    {
        var evaluations = entity.Evaluations
            .OrderBy(x => x.EvaluatedAtUtc)
            .Select(e => FraudEvaluationEntry.Rehydrate(
                e.Id,
                e.RuleCode,
                e.Passed,
                e.Score,
                e.Message,
                e.EvaluatedAtUtc))
            .ToList();

        return Transaction.Rehydrate(
            entity.Id,
            IdempotencyKey.Create(entity.IdempotencyKey),
            entity.ExternalReference,
            entity.MerchantId,
            entity.CustomerId,
            Money.Create(entity.Amount, entity.Currency),
            entity.PaymentMethod,
            entity.IpAddress,
            entity.DeviceFingerprint,
            (TransactionStatus)entity.Status,
            (FraudDecision)entity.Decision,
            entity.DecisionReason,
            entity.ProcessingAttempts,
            entity.CreatedAtUtc,
            entity.CompletedAtUtc,
            evaluations);
    }

    public static TransactionEntity ToEntity(Transaction transaction, TransactionEntity? existing = null)
    {
        var entity = existing ?? new TransactionEntity { Id = transaction.Id };
        entity.IdempotencyKey = transaction.IdempotencyKey.Value;
        entity.ExternalReference = transaction.ExternalReference;
        entity.MerchantId = transaction.MerchantId;
        entity.CustomerId = transaction.CustomerId;
        entity.Amount = transaction.Amount.Amount;
        entity.Currency = transaction.Amount.Currency;
        entity.PaymentMethod = transaction.PaymentMethod;
        entity.IpAddress = transaction.IpAddress;
        entity.DeviceFingerprint = transaction.DeviceFingerprint;
        entity.Status = (int)transaction.Status;
        entity.Decision = (int)transaction.Decision;
        entity.DecisionReason = transaction.DecisionReason;
        entity.ProcessingAttempts = transaction.ProcessingAttempts;
        entity.CreatedAtUtc = transaction.CreatedAtUtc;
        entity.CompletedAtUtc = transaction.CompletedAtUtc;

        if (existing is null)
        {
            entity.Evaluations = transaction.Evaluations.Select(e => new FraudEvaluationEntity
            {
                Id = e.Id,
                TransactionId = transaction.Id,
                RuleCode = e.RuleCode,
                Passed = e.Passed,
                Score = e.Score,
                Message = e.Message,
                EvaluatedAtUtc = e.EvaluatedAtUtc
            }).ToList();
        }
        else if (transaction.Evaluations.Count > 0)
        {
            foreach (var evaluation in transaction.Evaluations)
            {
                if (existing.Evaluations.All(x => x.Id != evaluation.Id))
                {
                    existing.Evaluations.Add(new FraudEvaluationEntity
                    {
                        Id = evaluation.Id,
                        TransactionId = transaction.Id,
                        RuleCode = evaluation.RuleCode,
                        Passed = evaluation.Passed,
                        Score = evaluation.Score,
                        Message = evaluation.Message,
                        EvaluatedAtUtc = evaluation.EvaluatedAtUtc
                    });
                }
            }
        }

        return entity;
    }
}
