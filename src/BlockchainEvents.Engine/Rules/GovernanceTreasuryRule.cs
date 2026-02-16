namespace BlockchainEvents.Engine.Rules;

public sealed class GovernanceTreasuryRuleOptions
{
    public const string SectionName = "Rules:GovernanceTreasury";
    public bool Enabled { get; set; } = true;
    public bool IncludeGovernanceActions { get; set; } = true;
    public bool IncludeTreasuryWithdrawals { get; set; } = true;
    public bool IncludeDelegations { get; set; } = false;
    public bool IncludeStakeRegistrations { get; set; } = false;
    public bool IncludeVotes { get; set; } = true;
}

public sealed class GovernanceTreasuryRule(IOptions<GovernanceTreasuryRuleOptions> options) : TransactionRuleBase
{
    private readonly GovernanceTreasuryRuleOptions _options = options.Value;

    public override string Id => "governance-treasury";
    public override string Name => "Governance & Treasury";
    public override string Description => "Matches governance-related transactions and treasury withdrawals";
    public override bool IsEnabled => _options.Enabled &&
        (_options.IncludeGovernanceActions || _options.IncludeTreasuryWithdrawals ||
         _options.IncludeDelegations || _options.IncludeStakeRegistrations || _options.IncludeVotes);

    public override bool IsMatch(TransactionData transaction, RuleContext context) =>
        (_options.IncludeGovernanceActions && transaction.HasGovernanceAction) ||
        (_options.IncludeTreasuryWithdrawals && transaction.HasTreasuryWithdrawal) ||
        (_options.IncludeDelegations && transaction.HasStakeDelegation) ||
        (_options.IncludeStakeRegistrations && transaction.HasStakeRegistration) ||
        (_options.IncludeVotes && transaction.HasVote);

    public override RuleMatchResult Evaluate(TransactionData transaction, RuleContext context)
    {
        var matchTypes = new List<string>();
        if (_options.IncludeGovernanceActions && transaction.HasGovernanceAction) matchTypes.Add("governance_action");
        if (_options.IncludeTreasuryWithdrawals && transaction.HasTreasuryWithdrawal) matchTypes.Add("treasury_withdrawal");
        if (_options.IncludeDelegations && transaction.HasStakeDelegation) matchTypes.Add("stake_delegation");
        if (_options.IncludeStakeRegistrations && transaction.HasStakeRegistration) matchTypes.Add("stake_registration");
        if (_options.IncludeVotes && transaction.HasVote) matchTypes.Add("vote");

        return new RuleMatchResult(Id, Name, new Dictionary<string, object> { ["governance_types"] = matchTypes });
    }
}
