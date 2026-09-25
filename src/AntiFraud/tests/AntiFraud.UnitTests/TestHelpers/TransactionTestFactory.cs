using AntiFraud.Domain.Transactions;

namespace AntiFraud.UnitTests.TestHelpers;

internal static class TransactionTestFactory
{
    public static Transaction CreateQueued(
        decimal amount,
        string customerId = "CUS-UNIT",
        string? idempotencyKey = null)
    {
        var key = IdempotencyKey.Create(idempotencyKey ?? $"unit-key-{Guid.NewGuid():N}"[..16]);
        var transaction = Transaction.Receive(
            key,
            externalReference: "ORD-UNIT-1",
            merchantId: "MRC-UNIT",
            customerId,
            Money.Create(amount, "BRL"),
            paymentMethod: "PIX",
            ipAddress: null,
            deviceFingerprint: null);

        transaction.MarkQueued();
        return transaction;
    }
}
