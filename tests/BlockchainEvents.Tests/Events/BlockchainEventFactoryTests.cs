namespace BlockchainEvents.Tests.Events;

public class BlockchainEventFactoryTests
{
    [Fact]
    public void Create_CreatesValidBlockchainEvent()
    {
        // Arrange
        var transaction = CreateTransaction("tx-abc123");
        var ruleResult = new RuleMatchResult(
            RuleId: "address-match",
            RuleName: "Address Match Rule",
            MatchedCriteria: new Dictionary<string, object>
            {
                ["matched_addresses"] = new[] { "addr_test1qz123" }
            }
        );
        var context = new RuleContext(
            Slot: 12345678,
            BlockHash: "blockhash123",
            BlockHeight: 9876543,
            Era: "Conway",
            Network: "preprod",
            BlockTime: DateTimeOffset.Parse("2024-01-15T10:30:00Z")
        );

        // Act
        var result = BlockchainEventFactory.Create(ruleResult, transaction, context);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().NotBeEmpty();
        result.Source.Should().Contain("preprod");
        result.Type.Should().Contain("address-match");
        result.Subject.Should().Be("Address Match Rule");
    }

    [Fact]
    public void Create_IncludesCardanoExtensions()
    {
        // Arrange
        var transaction = CreateTransaction("tx-def456");
        var ruleResult = new RuleMatchResult(
            RuleId: "policy-id-asset",
            RuleName: "Policy ID Asset Rule",
            MatchedCriteria: new Dictionary<string, object>
            {
                ["matched_policies"] = new[] { "policy123" }
            }
        );
        var context = new RuleContext(
            Slot: 99999999,
            BlockHash: "blockXYZ",
            BlockHeight: 5000000,
            Era: "Conway",
            Network: "mainnet",
            BlockTime: DateTimeOffset.UtcNow
        );

        // Act
        var result = BlockchainEventFactory.Create(ruleResult, transaction, context);

        // Assert
        result.CardanoSlot.Should().Be(99999999);
        result.CardanoBlock.Should().Be("blockXYZ");
        result.CardanoBlockHeight.Should().Be(5000000);
        result.CardanoEra.Should().Be("Conway");
        result.CardanoNetwork.Should().Be("mainnet");
    }

    [Fact]
    public void Create_IncludesRuleDataInPayload()
    {
        // Arrange
        var transaction = CreateTransaction("tx-ghi789");
        var ruleResult = new RuleMatchResult(
            RuleId: "metadata-key-value",
            RuleName: "Metadata Key Value Rule",
            MatchedCriteria: new Dictionary<string, object>
            {
                ["matched_labels"] = new[] { 721, 674 }
            }
        );
        var context = new RuleContext(
            Slot: 12345678,
            BlockHash: "blockhash",
            BlockHeight: 9876543,
            Era: "Conway",
            Network: "preprod",
            BlockTime: DateTimeOffset.UtcNow
        );

        // Act
        var result = BlockchainEventFactory.Create(ruleResult, transaction, context);

        // Assert
        result.Data.Should().NotBeNull();
        result.Data.TransactionId.Should().Be("tx-ghi789");
        result.Data.RuleId.Should().Be("metadata-key-value");
        result.Data.RuleName.Should().Be("Metadata Key Value Rule");
    }

    [Fact]
    public void Create_GeneratesUniqueIds()
    {
        // Arrange
        var transaction = CreateTransaction("tx-123");
        var ruleResult = new RuleMatchResult(
            RuleId: "test-rule",
            RuleName: "Test Rule",
            MatchedCriteria: new Dictionary<string, object>()
        );
        var context = new RuleContext(
            Slot: 12345678,
            BlockHash: "blockhash",
            BlockHeight: 9876543,
            Era: "Conway",
            Network: "preprod",
            BlockTime: DateTimeOffset.UtcNow
        );

        // Act
        var event1 = BlockchainEventFactory.Create(ruleResult, transaction, context);
        // Add small delay for unique timestamp
        System.Threading.Thread.Sleep(1);
        var event2 = BlockchainEventFactory.Create(ruleResult, transaction, context);

        // Assert
        event1.Id.Should().NotBe(event2.Id);
    }

    [Fact]
    public void Create_SetsCorrectEventType()
    {
        // Arrange
        var transaction = CreateTransaction("tx-123");
        var ruleResult = new RuleMatchResult(
            RuleId: "test-rule",
            RuleName: "Test Rule",
            MatchedCriteria: new Dictionary<string, object>()
        );
        var context = new RuleContext(
            Slot: 12345678,
            BlockHash: "blockhash",
            BlockHeight: 9876543,
            Era: "Conway",
            Network: "preprod",
            BlockTime: DateTimeOffset.UtcNow
        );

        // Act
        var result = BlockchainEventFactory.Create(ruleResult, transaction, context);

        // Assert
        result.Type.Should().Be("io.cardano.transaction.test-rule");
    }

    private static TransactionData CreateTransaction(string id)
    {
        return new TransactionData
        {
            Id = id,
            Slot = 12345678,
            BlockHash = "abc123",
            BlockHeight = 9876543,
            Index = 0,
            Fee = 200000,
            InputAddresses = ["addr_test1qz111"],
            OutputAddresses = ["addr_test1qz222", "addr_test1qz333"]
        };
    }
}
