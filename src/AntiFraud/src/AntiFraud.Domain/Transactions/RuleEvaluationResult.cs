namespace AntiFraud.Domain.Transactions;

public sealed record RuleEvaluationResult(string RuleCode, bool Passed, int Score, string? Message);
