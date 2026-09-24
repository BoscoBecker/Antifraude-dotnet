using AntiFraud.Domain.Transactions;

namespace AntiFraud.Domain.Rules;

public sealed class HighAmountRule : IFraudRule
{
    private const decimal Threshold = 10_000m;
    public string Code => "HIGH_AMOUNT";
    public int Order => 10;

    public RuleEvaluationResult Evaluate(Transaction transaction)
    {
        if (transaction.Amount.Amount >= Threshold)
            return new RuleEvaluationResult(Code, false, 60, $"Amount {transaction.Amount.Amount} >= {Threshold}.");

        return new RuleEvaluationResult(Code, true, 0, null);
    }
}
