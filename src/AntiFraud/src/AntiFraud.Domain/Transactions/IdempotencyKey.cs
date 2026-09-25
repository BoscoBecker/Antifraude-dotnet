namespace AntiFraud.Domain.Transactions;

public sealed record IdempotencyKey
{
    public string Value { get; }
    private IdempotencyKey(string value) => Value = value;

    public static IdempotencyKey Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Idempotency key is required.", nameof(value));

        var trimmed = value.Trim();
        if (trimmed.Length is < 8 or > 128)
            throw new ArgumentException("Idempotency key must be between 8 and 128 characters.", nameof(value));

        return new IdempotencyKey(trimmed);
    }
}
