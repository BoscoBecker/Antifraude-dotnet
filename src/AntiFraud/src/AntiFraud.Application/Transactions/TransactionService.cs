using AntiFraud.Application.Abstractions;
using AntiFraud.Application.Contracts;
using AntiFraud.Domain.Repositories;
using AntiFraud.Domain.Transactions;
using Microsoft.Extensions.Logging;

namespace AntiFraud.Application.Transactions;

public sealed class TransactionService : ITransactionService
{
    private readonly ITransactionRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOutboxStore _outboxStore;
    private readonly IAuditLogger _auditLogger;
    private readonly ILogger<TransactionService> _logger;

    public TransactionService(
        ITransactionRepository repository,
        IUnitOfWork unitOfWork,
        IOutboxStore outboxStore,
        IAuditLogger auditLogger,
        ILogger<TransactionService> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _outboxStore = outboxStore;
        _auditLogger = auditLogger;
        _logger = logger;
    }

    public async Task<(TransactionResponse Response, bool Created)> SubmitAsync(
        string idempotencyKey,
        SubmitTransactionRequest request,
        CancellationToken cancellationToken)
    {
        var key = IdempotencyKey.Create(idempotencyKey);
        var existing = await _repository.GetByIdempotencyKeyAsync(key.Value, cancellationToken);
        if (existing is not null)
        {
            _logger.LogInformation("Idempotent replay for key {IdempotencyKey}, transaction {TransactionId}", key.Value, existing.Id);
            return (TransactionMapper.ToResponse(existing), false);
        }

        var amount = Money.Create(request.Amount, request.Currency);
        var transaction = Transaction.Receive(
            key,
            request.ExternalReference,
            request.MerchantId,
            request.CustomerId,
            amount,
            request.PaymentMethod,
            request.IpAddress,
            request.DeviceFingerprint);

        transaction.MarkQueued();

        await _repository.AddAsync(transaction, cancellationToken);
        await _auditLogger.LogAsync("TRANSACTION_RECEIVED", transaction.Id, key.Value, cancellationToken);
        await _outboxStore.EnqueueTransactionReceivedAsync(transaction.Id, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (TransactionMapper.ToResponse(transaction), true);
    }

    public async Task<TransactionResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var transaction = await _repository.GetByIdAsync(id, cancellationToken);
        return transaction is null ? null : TransactionMapper.ToResponse(transaction);
    }
}
