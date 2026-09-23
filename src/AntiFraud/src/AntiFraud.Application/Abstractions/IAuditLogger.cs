namespace AntiFraud.Application.Abstractions;

public interface IAuditLogger
{
    Task LogAsync(string action, Guid? transactionId, string payload, CancellationToken cancellationToken);
}
