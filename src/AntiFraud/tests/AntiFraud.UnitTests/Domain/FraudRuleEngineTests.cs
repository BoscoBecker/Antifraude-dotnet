using AntiFraud.Domain.Rules;
using AntiFraud.Domain.Transactions;
using AntiFraud.UnitTests.TestHelpers;
using Xunit;

namespace AntiFraud.UnitTests.Domain;

public sealed class FraudRuleEngineTests
{
    private static FraudRuleEngine CreateEngine() =>
        new([new HighAmountRule(), new VelocityRule()]);

    [Fact]
    public void Evaluate_LowAmount_ReturnsApproved()
    {
        var engine = CreateEngine();
        var transaction = TransactionTestFactory.CreateQueued(500);

        var (decision, _, results) = engine.Evaluate(transaction);

        Assert.Equal(FraudDecision.Approved, decision);
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.True(r.Passed));
    }

    [Fact]
    public void Evaluate_HighAmountOnly_ReturnsReview()
    {
        var engine = CreateEngine();
        var transaction = TransactionTestFactory.CreateQueued(10_000);

        var (decision, reason, results) = engine.Evaluate(transaction);

        Assert.Equal(FraudDecision.Review, decision);
        Assert.Contains("60", reason);
        Assert.Contains(results, r => r.RuleCode == "HIGH_AMOUNT" && !r.Passed && r.Score == 60);
    }

    [Fact]
    public void Evaluate_BlocklistRule_ReturnsRejectedRegardlessOfScore()
    {
        var blocklist = new FakeBlocklistRule();
        var engine = new FraudRuleEngine([new HighAmountRule(), blocklist]);
        var transaction = TransactionTestFactory.CreateQueued(100);

        var (decision, reason, _) = engine.Evaluate(transaction);

        Assert.Equal(FraudDecision.Rejected, decision);
        Assert.Contains("blocklist", reason, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FakeBlocklistRule : IFraudRule
    {
        public string Code => "BLOCKLIST";
        public int Order => 5;

        public RuleEvaluationResult Evaluate(Transaction transaction) =>
            new(Code, false, 10, "Blocked.");
    }
}
