using AntiFraud.Domain.Transactions;

namespace AntiFraud.Domain.Rules;

/// <summary>
/// Regra simplificada para o desafio; em produção consultaria agregados via repositório de leitura.
/// </summary>
public sealed class VelocityRule : IFraudRule
{
    public string Code => "VELOCITY";
    public int Order => 20;

    public RuleEvaluationResult Evaluate(Transaction transaction)
    {
        // Placeholder: worker injeta contexto real via decorator ou specification no Application layer.
        return new RuleEvaluationResult(Code, true, 0, "Velocity check delegated to infrastructure.");
    }
}
