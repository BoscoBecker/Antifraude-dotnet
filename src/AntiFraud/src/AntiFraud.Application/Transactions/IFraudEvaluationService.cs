namespace AntiFraud.Application.Transactions;

public interface IFraudEvaluationService
{
    Task ProcessAsync(Guid transactionId, CancellationToken cancellationToken);
}
