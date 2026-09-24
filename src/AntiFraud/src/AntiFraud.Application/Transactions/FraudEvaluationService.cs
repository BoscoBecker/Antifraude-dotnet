using AntiFraud.Application.Abstractions;
using AntiFraud.Domain.Repositories;
using AntiFraud.Domain.Rules;
using AntiFraud.Domain.Transactions;
using Microsoft.Extensions.Logging;

namespace AntiFraud.Application.Transactions;

public sealed class FraudEvaluationService(
    ITransactionRepository repository,
    IUnitOfWork unitOfWork,
    FraudRuleEngine ruleEngine,
    IAuditLogger auditLogger,
    ILogger<FraudEvaluationService> logger) : IFraudEvaluationService
{
    private const int VelocityThreshold = 5;
    private static readonly TimeSpan VelocityWindow = TimeSpan.FromMinutes(10);

    private readonly ITransactionRepository _repository = repository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly FraudRuleEngine _ruleEngine = ruleEngine;
    private readonly IAuditLogger _auditLogger = auditLogger;
    private readonly ILogger<FraudEvaluationService> _logger = logger;

    public async Task ProcessAsync(Guid transactionId, CancellationToken cancellationToken)
    {
        var transaction = await _repository.GetByIdAsync(transactionId, cancellationToken);
        if (transaction is null)
        {
            _logger.LogWarning("Transaction {TransactionId} not found for evaluation", transactionId);
            return;
        }

        if (transaction.Status == TransactionStatus.Completed)
        {
            return;
        }

        /// Start transaction
        transaction.BeginProcessing();

        var recentCount = await _repository.CountRecentByCustomerAsync(
            transaction.CustomerId,
            VelocityWindow,
            cancellationToken);

        var (decision, reason, results) = _ruleEngine.Evaluate(transaction);
        var mergedResults = results.ToList();

        if (recentCount >= VelocityThreshold)
        {
            mergedResults.Add(new RuleEvaluationResult(
                "VELOCITY",
                false,
                70,
                $"{recentCount} transactions in {VelocityWindow.TotalMinutes} minutes."));

            var totalScore = mergedResults.Where(r => !r.Passed).Sum(r => r.Score);
            (decision, reason) = totalScore switch
            {
                >= 100 => (FraudDecision.Rejected, $"Risk score {totalScore} exceeded threshold."),
                >= 50 => (FraudDecision.Review, $"Risk score {totalScore} requires manual review."),
                _ => (FraudDecision.Approved, reason)
            };
        }
        transaction.CompleteEvaluation(decision, reason, mergedResults);
        /// End transaction
        
        await _repository.UpdateAsync(transaction, cancellationToken);
        await _auditLogger.LogAsync("TRANSACTION_EVALUATED", transaction.Id, decision.ToString(), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Transaction {TransactionId} evaluated as {Decision}",transactionId,decision);
    }
}
