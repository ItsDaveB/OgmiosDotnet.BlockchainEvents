namespace BlockchainEvents.Tests.Rules;

public class MetadataKeyValueRuleTests
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
    public void IsMatch_WithMatchingLabel_ReturnsTrue()
    {
        // Arrange
        var options = Options.Create(new MetadataKeyValueRuleOptions
        {
            Enabled = true,
            Labels = [721]  // CIP-25 NFT metadata
        });
        var rule = new MetadataKeyValueRule(options);
        var transaction = CreateTransactionWithMetadata(new Dictionary<int, object?>
        {
            [721] = new { policyId = "abc123", name = "MyNFT" }
        });

        // Act
        var result = rule.IsMatch(transaction, _context);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsMatch_WithMatchingKeyPattern_ReturnsTrue()
    {
        // Arrange
        var options = Options.Create(new MetadataKeyValueRuleOptions
        {
            Enabled = true,
            KeyPatterns = ["name"]
        });
        var rule = new MetadataKeyValueRule(options);
        var transaction = CreateTransactionWithMetadata(new Dictionary<int, object?>
        {
            [674] = new Dictionary<string, object> { ["name"] = "TestName" }
        });

        // Act
        var result = rule.IsMatch(transaction, _context);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsMatch_WithMatchingValuePattern_ReturnsTrue()
    {
        // Arrange
        var options = Options.Create(new MetadataKeyValueRuleOptions
        {
            Enabled = true,
            ValuePatterns = ["ISPO"]
        });
        var rule = new MetadataKeyValueRule(options);
        var transaction = CreateTransactionWithMetadata(new Dictionary<int, object?>
        {
            [674] = new Dictionary<string, object>
            {
                ["msg"] = new List<string> { "Welcome to ISPO participation!" }
            }
        });

        // Act
        var result = rule.IsMatch(transaction, _context);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsMatch_WithNoMatchingLabel_ReturnsFalse()
    {
        // Arrange
        var options = Options.Create(new MetadataKeyValueRuleOptions
        {
            Enabled = true,
            Labels = [999]
        });
        var rule = new MetadataKeyValueRule(options);
        var transaction = CreateTransactionWithMetadata(new Dictionary<int, object?>
        {
            [721] = new { name = "test" }
        });

        // Act
        var result = rule.IsMatch(transaction, _context);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_ReturnsMatchedLabelsAndPatterns()
    {
        // Arrange
        var options = Options.Create(new MetadataKeyValueRuleOptions
        {
            Enabled = true,
            Labels = [721, 674],
            KeyPatterns = ["msg"]
        });
        var rule = new MetadataKeyValueRule(options);
        var transaction = CreateTransactionWithMetadata(new Dictionary<int, object?>
        {
            [674] = new Dictionary<string, object> { ["msg"] = "Hello" },
            [721] = new { name = "NFT" }
        });

        // Act
        var result = rule.Evaluate(transaction, _context);

        // Assert
        result.Should().NotBeNull();
        result.RuleId.Should().Be("metadata-key-value");
        result.MatchedCriteria.Should().ContainKey("matched_labels");
    }

    [Fact]
    public void IsMatch_WhenDisabled_RuleNotEnabled()
    {
        // Arrange
        var options = Options.Create(new MetadataKeyValueRuleOptions
        {
            Enabled = false,
            Labels = [721]
        });
        var rule = new MetadataKeyValueRule(options);

        // Act & Assert
        rule.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void IsMatch_WithEmptyMetadata_ReturnsFalse()
    {
        // Arrange
        var options = Options.Create(new MetadataKeyValueRuleOptions
        {
            Enabled = true,
            Labels = [721]
        });
        var rule = new MetadataKeyValueRule(options);
        var transaction = CreateTransactionWithMetadata(new Dictionary<int, object?>());

        // Act
        var result = rule.IsMatch(transaction, _context);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsMatch_WithMatchAny_MatchesAnyMetadata()
    {
        // Arrange
        var options = Options.Create(new MetadataKeyValueRuleOptions
        {
            Enabled = true,
            MatchAny = true
        });
        var rule = new MetadataKeyValueRule(options);
        var transaction = CreateTransactionWithMetadata(new Dictionary<int, object?>
        {
            [999] = new { something = "arbitrary" }
        });

        // Act
        var result = rule.IsMatch(transaction, _context);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsMatch_WithMatchAny_EmptyMetadata_ReturnsFalse()
    {
        // Arrange
        var options = Options.Create(new MetadataKeyValueRuleOptions
        {
            Enabled = true,
            MatchAny = true
        });
        var rule = new MetadataKeyValueRule(options);
        var transaction = CreateTransactionWithMetadata(new Dictionary<int, object?>());

        // Act
        var result = rule.IsMatch(transaction, _context);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsEnabled_WithMatchAnyOnly_ReturnsTrue()
    {
        // Arrange — no labels, patterns, or values; just MatchAny
        var options = Options.Create(new MetadataKeyValueRuleOptions
        {
            Enabled = true,
            MatchAny = true
        });
        var rule = new MetadataKeyValueRule(options);

        // Act & Assert
        rule.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_WithMatchAny_IncludesMatchModeInCriteria()
    {
        // Arrange
        var options = Options.Create(new MetadataKeyValueRuleOptions
        {
            Enabled = true,
            MatchAny = true
        });
        var rule = new MetadataKeyValueRule(options);
        var transaction = CreateTransactionWithMetadata(new Dictionary<int, object?>
        {
            [674] = new Dictionary<string, object> { ["msg"] = "Hello" }
        });

        // Act
        var result = rule.Evaluate(transaction, _context);

        // Assert
        result.Should().NotBeNull();
        result.MatchedCriteria.Should().ContainKey("match_mode");
        result.MatchedCriteria["match_mode"].Should().Be("any_metadata");
    }

    private static TransactionData CreateTransactionWithMetadata(
        Dictionary<int, object?> metadata)
    {
        return new TransactionData
        {
            Id = "tx-123",
            Slot = 12345678,
            BlockHash = "abc123",
            BlockHeight = 9876543,
            Index = 0,
            Metadata = metadata
        };
    }
}
