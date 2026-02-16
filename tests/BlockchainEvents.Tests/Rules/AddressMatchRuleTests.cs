namespace BlockchainEvents.Tests.Rules;

public class AddressMatchRuleTests
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
    public void IsMatch_WithMatchingAddress_ReturnsTrue()
    {
        // Arrange
        var options = Options.Create(new AddressMatchRuleOptions
        {
            Enabled = true,
            Addresses = ["addr_test1qz123"]
        });
        var rule = new AddressMatchRule(options);
        var transaction = CreateTransaction(outputAddresses: ["addr_test1qz123", "addr_test1qz456"]);

        // Act
        var result = rule.IsMatch(transaction, _context);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsMatch_WithMatchingPrefix_ReturnsTrue()
    {
        // Arrange
        var options = Options.Create(new AddressMatchRuleOptions
        {
            Enabled = true,
            Prefixes = ["addr_test1"]
        });
        var rule = new AddressMatchRule(options);
        var transaction = CreateTransaction(outputAddresses: ["addr_test1qz123"]);

        // Act
        var result = rule.IsMatch(transaction, _context);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsMatch_WithNoMatchingAddress_ReturnsFalse()
    {
        // Arrange
        var options = Options.Create(new AddressMatchRuleOptions
        {
            Enabled = true,
            Addresses = ["addr_test1qz999"]
        });
        var rule = new AddressMatchRule(options);
        var transaction = CreateTransaction(outputAddresses: ["addr_test1qz123"]);

        // Act
        var result = rule.IsMatch(transaction, _context);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsMatch_WhenDisabled_ReturnsFalse()
    {
        // Arrange
        var options = Options.Create(new AddressMatchRuleOptions
        {
            Enabled = false,
            Addresses = ["addr_test1qz123"]
        });
        var rule = new AddressMatchRule(options);
        var transaction = CreateTransaction(outputAddresses: ["addr_test1qz123"]);

        // Act & Assert
        rule.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_ReturnsMatchedAddresses()
    {
        // Arrange
        var options = Options.Create(new AddressMatchRuleOptions
        {
            Enabled = true,
            Addresses = ["addr_test1qz123", "addr_test1qz456"]
        });
        var rule = new AddressMatchRule(options);
        var transaction = CreateTransaction(outputAddresses: ["addr_test1qz123", "addr_test1qz789"]);

        // Act
        var result = rule.Evaluate(transaction, _context);

        // Assert
        result.Should().NotBeNull();
        result.RuleId.Should().Be("address-match");
        result.MatchedCriteria.Should().ContainKey("matched_addresses");
        var matchedAddresses = result.MatchedCriteria["matched_addresses"] as IEnumerable<string>;
        matchedAddresses.Should().NotBeNull();
        matchedAddresses.Should().Contain("addr_test1qz123");
    }

    private static TransactionData CreateTransaction(
        string id = "tx-123",
        IReadOnlyList<string>? inputAddresses = null,
        IReadOnlyList<string>? outputAddresses = null)
    {
        return new TransactionData
        {
            Id = id,
            Slot = 12345678,
            BlockHash = "abc123",
            BlockHeight = 9876543,
            Index = 0,
            InputAddresses = inputAddresses ?? [],
            OutputAddresses = outputAddresses ?? []
        };
    }
}
