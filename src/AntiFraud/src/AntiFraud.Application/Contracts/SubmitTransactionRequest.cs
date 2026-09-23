namespace AntiFraud.Application.Contracts;

public sealed record SubmitTransactionRequest(
    string ExternalReference,
    string MerchantId,
    string CustomerId,
    decimal Amount,
    string Currency,
    string PaymentMethod,
    string? IpAddress,
    string? DeviceFingerprint);
