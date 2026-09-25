using AntiFraud.Application.Abstractions;
using AntiFraud.Application.Transactions;
using AntiFraud.Domain.Repositories;
using AntiFraud.Domain.Rules;
using AntiFraud.Domain.Transactions;
using AntiFraud.UnitTests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace AntiFraud.UnitTests.Application;

public sealed class FraudEvaluationServiceTests
{
    private readonly Mock<ITransactionRepository> _repository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IAuditLogger> _audit = new();
    private readonly FraudRuleEngine _ruleEngine = new([new HighAmountRule(), new VelocityRule()]);

    private FraudEvaluationService CreateSut() =>
        new(
            _repository.Object,
            _unitOfWork.Object,
            _ruleEngine,
            _audit.Object,
            NullLogger<FraudEvaluationService>.Instance);

    [Fact]
    public async Task ProcessAsync_WhenAlreadyCompleted_DoesNotUpdate()
    {
        var completed = TransactionTestFactory.CreateQueued(100);
        completed.BeginProcessing();
        completed.CompleteEvaluation(
            FraudDecision.Approved,
            "done",
            [new RuleEvaluationResult("HIGH_AMOUNT", true, 0, null)]);

        _repository.Setup(r => r.GetByIdAsync(completed.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(completed);

        await CreateSut().ProcessAsync(completed.Id, CancellationToken.None);

        _repository.Verify(
            r => r.UpdateAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_VelocityThreshold_ReturnsReview()
    {
        var transaction = TransactionTestFactory.CreateQueued(50, customerId: "CUS-VEL");
        _repository.Setup(r => r.GetByIdAsync(transaction.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction);
        _repository.Setup(r => r.CountRecentByCustomerAsync("CUS-VEL", It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        await CreateSut().ProcessAsync(transaction.Id, CancellationToken.None);

        Assert.Equal(FraudDecision.Review, transaction.Decision);
        Assert.Equal(TransactionStatus.Completed, transaction.Status);
        Assert.Contains(transaction.Evaluations, e => e.RuleCode == "VELOCITY" && !e.Passed && e.Score == 70);
        _repository.Verify(r => r.UpdateAsync(transaction, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_HighAmountAndVelocity_ReturnsRejected()
    {
        var transaction = TransactionTestFactory.CreateQueued(15_000, customerId: "CUS-REJ");
        _repository.Setup(r => r.GetByIdAsync(transaction.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction);
        _repository.Setup(r => r.CountRecentByCustomerAsync("CUS-REJ", It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        await CreateSut().ProcessAsync(transaction.Id, CancellationToken.None);

        Assert.Equal(FraudDecision.Rejected, transaction.Decision);
        Assert.Contains(transaction.Evaluations, e => e.RuleCode == "HIGH_AMOUNT" && !e.Passed);
        Assert.Contains(transaction.Evaluations, e => e.RuleCode == "VELOCITY" && !e.Passed);
    }
}
