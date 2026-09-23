namespace AntiFraud.Domain.Transactions;

public enum TransactionStatus
{
    Received = 0,
    Queued = 1,
    Processing = 2,
    Completed = 3,
    Failed = 4
}
