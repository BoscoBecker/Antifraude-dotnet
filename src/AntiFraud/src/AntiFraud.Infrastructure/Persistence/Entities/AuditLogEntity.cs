namespace AntiFraud.Infrastructure.Persistence.Entities;

public sealed class AuditLogEntity
{
    public Guid Id { get; set; }
    public string Action { get; set; } = null!;
    public Guid? TransactionId { get; set; }
    public string Payload { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; }
}
