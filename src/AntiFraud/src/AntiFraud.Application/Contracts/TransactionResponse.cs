namespace AntiFraud.Application.Contracts;

public sealed record TransactionResponse(
    Guid Id,
    string Status,
    string Decision,
    string? DecisionReason,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc,
    IReadOnlyList<RuleEvaluationDto> Evaluations);

public sealed record RuleEvaluationDto(string RuleCode, bool Passed, int Score, string? Message);
