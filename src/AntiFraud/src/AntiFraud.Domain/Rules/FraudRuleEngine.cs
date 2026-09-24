using AntiFraud.Domain.Transactions;

namespace AntiFraud.Domain.Rules;

public sealed class FraudRuleEngine(IEnumerable<IFraudRule> rules)
{
    private readonly IReadOnlyList<IFraudRule> _rules = rules.OrderBy(r => r.Order).ToList();

    public (FraudDecision Decision, string Reason, IReadOnlyList<RuleEvaluationResult> Results) Evaluate(Transaction transaction)
    {
        var results = new List<RuleEvaluationResult>();
        var totalScore = 0;

        foreach (var rule in _rules)
        {
            var result = rule.Evaluate(transaction);
            results.Add(result);
            if (!result.Passed)
            {
                totalScore += result.Score;
            }
        }

        if (results.Any(r => r.RuleCode == "BLOCKLIST" && !r.Passed))
            return (FraudDecision.Rejected, "Customer or device on blocklist.", results);
        if (totalScore >= 100)
            return (FraudDecision.Rejected, $"Risk score {totalScore} exceeded threshold.", results);
        if (totalScore >= 50)
            return (FraudDecision.Review, $"Risk score {totalScore} requires manual review.", results);

        return (FraudDecision.Approved, "All rules passed within acceptable risk.", results);
    }
}
