using AntiFraud.Application.Contracts;
using AntiFraud.Domain.Transactions;

namespace AntiFraud.Application.Transactions;

internal static class TransactionMapper
{
    public static TransactionResponse ToResponse(Transaction transaction) =>
        new(
            transaction.Id,
            transaction.Status.ToString().ToUpperInvariant(),
            MapDecision(transaction.Decision),
            transaction.DecisionReason,
            transaction.CreatedAtUtc,
            transaction.CompletedAtUtc,
            transaction.Evaluations.Select(e => new RuleEvaluationDto(e.RuleCode, e.Passed, e.Score, e.Message)).ToList());

    private static string MapDecision(FraudDecision decision) =>
        decision switch
        {
            FraudDecision.Pending  => "PENDING",
            FraudDecision.Approved => "APPROVED",
            FraudDecision.Rejected => "REJECTED",
            FraudDecision.Review   => "REVIEW",
            _ => decision.ToString().ToUpperInvariant()
        };
}
