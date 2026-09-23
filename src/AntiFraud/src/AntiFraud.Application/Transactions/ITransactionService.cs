using AntiFraud.Application.Contracts;

namespace AntiFraud.Application.Transactions;

public interface ITransactionService
{
    Task<(TransactionResponse Response, bool Created)> SubmitAsync(
        string idempotencyKey,
        SubmitTransactionRequest request,
        CancellationToken cancellationToken);

    Task<TransactionResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}
