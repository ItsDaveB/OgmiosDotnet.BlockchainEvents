namespace BlockchainEvents.Tests.Rules;

public class AllTransactionsRuleTests
{
    private readonly RuleContext _context = new(
        Slot: 12345678,
        BlockHash: "abc123",
        BlockHeight: 9876543,
        Era: "Conway",
        Network: "preprod",
        BlockTime: DateTimeOffset.UtcNow
    );

    [Fact]
    public void IsMatch_WhenEnabled_AlwaysReturnsTrue()
    {
        // Arrange
        var rule = CreateRule(enabled: true);
        var transaction = CreateTransaction();

        // Act & Assert
        rule.IsMatch(transaction, _context).Should().BeTrue();
    }

    [Fact]
    public void IsEnabled_WhenDisabled_ReturnsFalse()
    {
        // Arrange
        var rule = CreateRule(enabled: false);

        // Act & Assert
        rule.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void IsEnabled_WhenEnabled_ReturnsTrue()
    {
        // Arrange
        var rule = CreateRule(enabled: true);

        // Act & Assert
        rule.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_IncludesTransactionDetails()
    {
        // Arrange
        var rule = CreateRule(enabled: true);
        var transaction = new TransactionData
        {
            Id = "tx-abc",
            Slot = 12345678,
            BlockHash = "abc123",
            BlockHeight = 9876543,
            Index = 0,
            InputAddresses = ["addr1_input"],
            OutputAddresses = ["addr1_out1", "addr1_out2"]
        };

        // Act
        var result = rule.Evaluate(transaction, _context);

        // Assert
        result.RuleId.Should().Be("all-transactions");
        result.RuleName.Should().Be("All Transactions");
        result.MatchedCriteria.Should().ContainKey("transaction_id");
        result.MatchedCriteria["transaction_id"].Should().Be("tx-abc");
        result.MatchedCriteria["input_count"].Should().Be(1);
        result.MatchedCriteria["output_count"].Should().Be(2);
    }

    [Fact]
    public void IsEnabled_DefaultsToFalse()
    {
        // Arrange — default options (no explicit Enabled = true)
        var options = Options.Create(new AllTransactionsRuleOptions());
        var rule = new AllTransactionsRule(options);

        // Act & Assert
        rule.IsEnabled.Should().BeFalse();
    }

    private static AllTransactionsRule CreateRule(bool enabled)
    {
        var options = Options.Create(new AllTransactionsRuleOptions { Enabled = enabled });
        return new AllTransactionsRule(options);
    }

    private static TransactionData CreateTransaction()
    {
        return new TransactionData
        {
            Id = "tx-123",
            Slot = 12345678,
            BlockHash = "abc123",
            BlockHeight = 9876543,
            Index = 0
        };
    }
}
