using AntiFraud.Domain.Rules;
using AntiFraud.UnitTests.TestHelpers;
using Xunit;

namespace AntiFraud.UnitTests.Domain;

public sealed class HighAmountRuleTests
{
    private readonly HighAmountRule _sut = new();

    [Theory]
    [InlineData(9_999.99, true, 0)]
    [InlineData(10_000, false, 60)]
    [InlineData(50_000, false, 60)]
    public void Evaluate_AmountThreshold(decimal amount, bool expectedPassed, int expectedScore)
    {
        var transaction = TransactionTestFactory.CreateQueued(amount);

        var result = _sut.Evaluate(transaction);

        Assert.Equal("HIGH_AMOUNT", result.RuleCode);
        Assert.Equal(expectedPassed, result.Passed);
        Assert.Equal(expectedScore, result.Score);
    }
}
