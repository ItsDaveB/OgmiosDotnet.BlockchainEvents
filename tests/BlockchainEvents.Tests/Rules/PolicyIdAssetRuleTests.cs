namespace BlockchainEvents.Tests.Rules;

public class PolicyIdAssetRuleTests
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
    public void IsMatch_WithMatchingPolicyId_ReturnsTrue()
    {
        // Arrange
        var options = Options.Create(new PolicyIdAssetRuleOptions
        {
            Enabled = true,
            PolicyIds = ["abc123def456"]
        });
        var rule = new PolicyIdAssetRule(options);
        var transaction = CreateTransactionWithAssets(new Dictionary<string, Dictionary<string, long>>
        {
            ["abc123def456"] = new() { ["TokenA"] = 100 }
        });

        // Act
        var result = rule.IsMatch(transaction, _context);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsMatch_WithMatchingAssetName_ReturnsTrue()
    {
        // Arrange
        var options = Options.Create(new PolicyIdAssetRuleOptions
        {
            Enabled = true,
            AssetNames = ["MyToken"]
        });
        var rule = new PolicyIdAssetRule(options);
        var transaction = CreateTransactionWithAssets(new Dictionary<string, Dictionary<string, long>>
        {
            ["policy123"] = new() { ["MyToken"] = 50 }
        });

        // Act
        var result = rule.IsMatch(transaction, _context);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsMatch_WithNoMatchingPolicy_ReturnsFalse()
    {
        // Arrange
        var options = Options.Create(new PolicyIdAssetRuleOptions
        {
            Enabled = true,
            PolicyIds = ["nonexistent"]
        });
        var rule = new PolicyIdAssetRule(options);
        var transaction = CreateTransactionWithAssets(new Dictionary<string, Dictionary<string, long>>
        {
            ["abc123def456"] = new() { ["TokenA"] = 100 }
        });

        // Act
        var result = rule.IsMatch(transaction, _context);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_ReturnsMatchedPoliciesAndAssets()
    {
        // Arrange
        var options = Options.Create(new PolicyIdAssetRuleOptions
        {
            Enabled = true,
            PolicyIds = ["policy123"],
            AssetNames = ["TokenB"]
        });
        var rule = new PolicyIdAssetRule(options);
        var transaction = CreateTransactionWithAssets(new Dictionary<string, Dictionary<string, long>>
        {
            ["policy123"] = new() { ["TokenA"] = 100 },
            ["policy456"] = new() { ["TokenB"] = 200 }
        });

        // Act
        var result = rule.Evaluate(transaction, _context);

        // Assert
        result.Should().NotBeNull();
        result.RuleId.Should().Be("policy-id-asset");
        result.MatchedCriteria.Should().ContainKey("matched_policies");
        result.MatchedCriteria.Should().ContainKey("matched_assets");
    }

    [Fact]
    public void IsMatch_WhenDisabled_RuleNotEnabled()
    {
        // Arrange
        var options = Options.Create(new PolicyIdAssetRuleOptions
        {
            Enabled = false,
            PolicyIds = ["abc123"]
        });
        var rule = new PolicyIdAssetRule(options);

        // Act & Assert
        rule.IsEnabled.Should().BeFalse();
    }

    private static TransactionData CreateTransactionWithAssets(
        Dictionary<string, Dictionary<string, long>> mintedAssets)
    {
        return new TransactionData
        {
            Id = "tx-123",
            Slot = 12345678,
            BlockHash = "abc123",
            BlockHeight = 9876543,
            Index = 0,
            MintedAssets = mintedAssets.ToDictionary(
                kvp => kvp.Key,
                kvp => (IReadOnlyDictionary<string, long>)kvp.Value)
        };
    }
}
