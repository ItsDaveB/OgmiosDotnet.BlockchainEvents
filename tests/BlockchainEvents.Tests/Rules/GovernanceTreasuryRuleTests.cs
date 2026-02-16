namespace BlockchainEvents.Tests.Rules;

public class GovernanceTreasuryRuleTests
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
    public void IsMatch_WithTreasuryWithdrawal_ReturnsTrue()
    {
        // Arrange
        var options = Options.Create(new GovernanceTreasuryRuleOptions
        {
            Enabled = true,
            IncludeTreasuryWithdrawals = true
        });
        var rule = new GovernanceTreasuryRule(options);
        var transaction = CreateTransaction(hasTreasuryWithdrawal: true);

        // Act
        var result = rule.IsMatch(transaction, _context);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsMatch_WithStakeRegistration_ReturnsTrue()
    {
        // Arrange
        var options = Options.Create(new GovernanceTreasuryRuleOptions
        {
            Enabled = true,
            IncludeStakeRegistrations = true
        });
        var rule = new GovernanceTreasuryRule(options);
        var transaction = CreateTransaction(hasStakeRegistration: true);

        // Act
        var result = rule.IsMatch(transaction, _context);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsMatch_WithStakeDelegation_ReturnsTrue()
    {
        // Arrange
        var options = Options.Create(new GovernanceTreasuryRuleOptions
        {
            Enabled = true,
            IncludeDelegations = true
        });
        var rule = new GovernanceTreasuryRule(options);
        var transaction = CreateTransaction(hasStakeDelegation: true);

        // Act
        var result = rule.IsMatch(transaction, _context);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsMatch_WithGovernanceAction_ReturnsTrue()
    {
        // Arrange
        var options = Options.Create(new GovernanceTreasuryRuleOptions
        {
            Enabled = true,
            IncludeGovernanceActions = true
        });
        var rule = new GovernanceTreasuryRule(options);
        var transaction = CreateTransaction(hasGovernanceAction: true);

        // Act
        var result = rule.IsMatch(transaction, _context);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsMatch_WithVote_ReturnsTrue()
    {
        // Arrange
        var options = Options.Create(new GovernanceTreasuryRuleOptions
        {
            Enabled = true,
            IncludeVotes = true
        });
        var rule = new GovernanceTreasuryRule(options);
        var transaction = CreateTransaction(hasVote: true);

        // Act
        var result = rule.IsMatch(transaction, _context);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsMatch_WithNoMatchingCriteria_ReturnsFalse()
    {
        // Arrange
        var options = Options.Create(new GovernanceTreasuryRuleOptions
        {
            Enabled = true,
            IncludeTreasuryWithdrawals = true  // Looking for treasury, but transaction has delegation
        });
        var rule = new GovernanceTreasuryRule(options);
        var transaction = CreateTransaction(hasStakeDelegation: true);

        // Act
        var result = rule.IsMatch(transaction, _context);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_ReturnsMatchedGovernanceTypes()
    {
        // Arrange
        var options = Options.Create(new GovernanceTreasuryRuleOptions
        {
            Enabled = true,
            IncludeTreasuryWithdrawals = true,
            IncludeVotes = true
        });
        var rule = new GovernanceTreasuryRule(options);
        var transaction = CreateTransaction(hasTreasuryWithdrawal: true, hasVote: true);

        // Act
        var result = rule.Evaluate(transaction, _context);

        // Assert
        result.Should().NotBeNull();
        result.RuleId.Should().Be("governance-treasury");
        result.MatchedCriteria.Should().ContainKey("governance_types");
        var types = result.MatchedCriteria["governance_types"] as IEnumerable<string>;
        types.Should().Contain("treasury_withdrawal");
        types.Should().Contain("vote");
    }

    [Fact]
    public void IsMatch_WhenDisabled_RuleNotEnabled()
    {
        // Arrange
        var options = Options.Create(new GovernanceTreasuryRuleOptions
        {
            Enabled = false,
            IncludeTreasuryWithdrawals = true
        });
        var rule = new GovernanceTreasuryRule(options);

        // Act & Assert
        rule.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void IsMatch_WithAllCriteriaDisabled_ReturnsFalse()
    {
        // Arrange
        var options = Options.Create(new GovernanceTreasuryRuleOptions
        {
            Enabled = true,
            IncludeTreasuryWithdrawals = false,
            IncludeStakeRegistrations = false,
            IncludeDelegations = false,
            IncludeGovernanceActions = false,
            IncludeVotes = false
        });
        var rule = new GovernanceTreasuryRule(options);
        var transaction = CreateTransaction(hasTreasuryWithdrawal: true);

        // Act
        var result = rule.IsMatch(transaction, _context);

        // Assert
        result.Should().BeFalse();
    }

    private static TransactionData CreateTransaction(
        bool hasTreasuryWithdrawal = false,
        bool hasStakeRegistration = false,
        bool hasStakeDelegation = false,
        bool hasGovernanceAction = false,
        bool hasVote = false)
    {
        return new TransactionData
        {
            Id = "tx-123",
            Slot = 12345678,
            BlockHash = "abc123",
            BlockHeight = 9876543,
            Index = 0,
            HasTreasuryWithdrawal = hasTreasuryWithdrawal,
            HasStakeRegistration = hasStakeRegistration,
            HasStakeDelegation = hasStakeDelegation,
            HasGovernanceAction = hasGovernanceAction,
            HasVote = hasVote
        };
    }
}
