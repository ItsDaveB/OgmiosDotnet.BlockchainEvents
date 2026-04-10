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
    public override string Id => "governance-treasury";
    public override string Name => "Governance & Treasury";
    public override string Description => "Matches governance-related transactions and treasury withdrawals";
    public override bool IsEnabled => options.Value.Enabled &&
        (options.Value.IncludeGovernanceActions || options.Value.IncludeTreasuryWithdrawals ||
         options.Value.IncludeDelegations || options.Value.IncludeStakeRegistrations || options.Value.IncludeVotes);

    public override bool IsMatch(TransactionData transaction, RuleContext context) =>
        (options.Value.IncludeGovernanceActions && transaction.HasGovernanceAction) ||
        (options.Value.IncludeTreasuryWithdrawals && transaction.HasTreasuryWithdrawal) ||
        (options.Value.IncludeDelegations && transaction.HasStakeDelegation) ||
        (options.Value.IncludeStakeRegistrations && transaction.HasStakeRegistration) ||
        (options.Value.IncludeVotes && transaction.HasVote);

    public override RuleMatchResult Evaluate(TransactionData transaction, RuleContext context)
    {
        var matchTypes = new List<string>();
        if (options.Value.IncludeGovernanceActions && transaction.HasGovernanceAction) matchTypes.Add("governance_action");
        if (options.Value.IncludeTreasuryWithdrawals && transaction.HasTreasuryWithdrawal) matchTypes.Add("treasury_withdrawal");
        if (options.Value.IncludeDelegations && transaction.HasStakeDelegation) matchTypes.Add("stake_delegation");
        if (options.Value.IncludeStakeRegistrations && transaction.HasStakeRegistration) matchTypes.Add("stake_registration");
        if (options.Value.IncludeVotes && transaction.HasVote) matchTypes.Add("vote");

        return new RuleMatchResult(Id, Name, new Dictionary<string, object> { ["governance_types"] = matchTypes });
    }
}
