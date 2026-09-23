namespace AntiFraud.Domain.Transactions;

public sealed class FraudEvaluationEntry
{
    public Guid Id { get; private set; }
    public string RuleCode { get; private set; } = null!;
    public bool Passed { get; private set; }
    public int Score { get; private set; }
    public string? Message { get; private set; }
    public DateTime EvaluatedAtUtc { get; private set; }

    private FraudEvaluationEntry()
    {
    }

    public static FraudEvaluationEntry Create(string ruleCode, bool passed, int score, string? message) =>
        Rehydrate(Guid.NewGuid(), ruleCode, passed, score, message, DateTime.UtcNow);

    public static FraudEvaluationEntry Rehydrate(
        Guid id,
        string ruleCode,
        bool passed,
        int score,
        string? message,
        DateTime evaluatedAtUtc) =>
        new()
        {
            Id = id,
            RuleCode = ruleCode,
            Passed = passed,
            Score = score,
            Message = message,
            EvaluatedAtUtc = evaluatedAtUtc
        };
}
