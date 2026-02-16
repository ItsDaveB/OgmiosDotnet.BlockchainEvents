namespace BlockchainEvents.Tests.Engine;

public class RuleEngineTests
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
    public void MatchTransaction_WithMatchingRule_ReturnsMatchResult()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<RuleEngine>>();
        var mockRule = CreateMockRule("test-rule", "Test Rule", isMatch: true);
        var rules = new[] { mockRule.Object };
        var engine = new RuleEngine(rules, mockLogger.Object);
        var transaction = CreateTransaction();

        // Act
        var results = engine.MatchTransaction(transaction, _context).ToList();

        // Assert
        results.Should().HaveCount(1);
        results[0].RuleId.Should().Be("test-rule");
    }

    [Fact]
    public void MatchTransaction_WithNoMatchingRules_ReturnsEmpty()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<RuleEngine>>();
        var mockRule = CreateMockRule("test-rule", "Test Rule", isMatch: false);
        var rules = new[] { mockRule.Object };
        var engine = new RuleEngine(rules, mockLogger.Object);
        var transaction = CreateTransaction();

        // Act
        var results = engine.MatchTransaction(transaction, _context).ToList();

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public void MatchTransaction_WithMultipleMatchingRules_ReturnsAllMatches()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<RuleEngine>>();
        var rule1 = CreateMockRule("rule-1", "Rule 1", isMatch: true);
        var rule2 = CreateMockRule("rule-2", "Rule 2", isMatch: true);
        var rule3 = CreateMockRule("rule-3", "Rule 3", isMatch: false);
        var rules = new[] { rule1.Object, rule2.Object, rule3.Object };
        var engine = new RuleEngine(rules, mockLogger.Object);
        var transaction = CreateTransaction();

        // Act
        var results = engine.MatchTransaction(transaction, _context).ToList();

        // Assert
        results.Should().HaveCount(2);
        results.Select(r => r.RuleId).Should().Contain("rule-1");
        results.Select(r => r.RuleId).Should().Contain("rule-2");
    }

    [Fact]
    public void MatchTransaction_WithDisabledRule_SkipsRule()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<RuleEngine>>();
        var enabledRule = CreateMockRule("enabled-rule", "Enabled Rule", isMatch: true, isEnabled: true);
        var disabledRule = CreateMockRule("disabled-rule", "Disabled Rule", isMatch: true, isEnabled: false);
        var rules = new[] { enabledRule.Object, disabledRule.Object };
        var engine = new RuleEngine(rules, mockLogger.Object);
        var transaction = CreateTransaction();

        // Act
        var results = engine.MatchTransaction(transaction, _context).ToList();

        // Assert
        results.Should().HaveCount(1);
        results[0].RuleId.Should().Be("enabled-rule");
    }

    [Fact]
    public void MatchTransaction_WithNoRules_ReturnsEmpty()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<RuleEngine>>();
        var rules = Array.Empty<ITransactionRule>();
        var engine = new RuleEngine(rules, mockLogger.Object);
        var transaction = CreateTransaction();

        // Act
        var results = engine.MatchTransaction(transaction, _context).ToList();

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public void MatchTransactions_MatchesAllTransactions()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<RuleEngine>>();
        var mockRule = CreateMockRule("test-rule", "Test Rule", isMatch: true);
        var rules = new[] { mockRule.Object };
        var engine = new RuleEngine(rules, mockLogger.Object);
        var transactions = new[]
        {
            CreateTransaction("tx-1"),
            CreateTransaction("tx-2"),
            CreateTransaction("tx-3")
        };

        // Act
        var results = engine.MatchTransactions(transactions, _context).ToList();

        // Assert
        results.Should().HaveCount(3);
    }

    [Fact]
    public void RuleCount_ReturnsCorrectCount()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<RuleEngine>>();
        var rules = new[]
        {
            CreateMockRule("rule-1", "Rule 1", isMatch: false).Object,
            CreateMockRule("rule-2", "Rule 2", isMatch: false).Object
        };
        var engine = new RuleEngine(rules, mockLogger.Object);

        // Act
        var count = engine.RuleCount;

        // Assert
        count.Should().Be(2);
    }

    [Fact]
    public void EnabledRuleCount_ReturnsOnlyEnabledRules()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<RuleEngine>>();
        var rules = new[]
        {
            CreateMockRule("rule-1", "Rule 1", isMatch: false, isEnabled: true).Object,
            CreateMockRule("rule-2", "Rule 2", isMatch: false, isEnabled: false).Object,
            CreateMockRule("rule-3", "Rule 3", isMatch: false, isEnabled: true).Object
        };
        var engine = new RuleEngine(rules, mockLogger.Object);

        // Act
        var count = engine.EnabledRuleCount;

        // Assert
        count.Should().Be(2);
    }

    private static Mock<ITransactionRule> CreateMockRule(
        string id,
        string name,
        bool isMatch,
        bool isEnabled = true)
    {
        var mockRule = new Mock<ITransactionRule>();
        mockRule.Setup(r => r.Id).Returns(id);
        mockRule.Setup(r => r.Name).Returns(name);
        mockRule.Setup(r => r.IsEnabled).Returns(isEnabled);
        mockRule.Setup(r => r.IsMatch(It.IsAny<TransactionData>(), It.IsAny<RuleContext>())).Returns(isMatch);
        mockRule.Setup(r => r.Evaluate(It.IsAny<TransactionData>(), It.IsAny<RuleContext>()))
            .Returns(new RuleMatchResult(
                RuleId: id,
                RuleName: name,
                MatchedCriteria: new Dictionary<string, object> { ["test"] = "value" }
            ));
        return mockRule;
    }

    private static TransactionData CreateTransaction(string id = "tx-123")
    {
        return new TransactionData
        {
            Id = id,
            Slot = 12345678,
            BlockHash = "abc123",
            BlockHeight = 9876543,
            Index = 0
        };
    }
}
