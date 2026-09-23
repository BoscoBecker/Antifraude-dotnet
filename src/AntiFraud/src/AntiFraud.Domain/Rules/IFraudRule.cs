using AntiFraud.Domain.Transactions;

namespace AntiFraud.Domain.Rules;

/// <summary>
/// Open/Closed: novas regras implementam esta interface sem alterar o motor existente.
/// </summary>
public interface IFraudRule
{
    string Code { get; }
    int Order { get; }
    RuleEvaluationResult Evaluate(Transaction transaction);
}
