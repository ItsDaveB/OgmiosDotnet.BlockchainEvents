namespace BlockchainEvents.Engine.Rules;

public sealed class PolicyIdAssetRuleOptions
{
    public const string SectionName = "Rules:PolicyIdAsset";
    public bool Enabled { get; set; } = true;
    public List<string> PolicyIds { get; set; } = [];
    public List<string> AssetNames { get; set; } = [];
}

public sealed class PolicyIdAssetRule(IOptions<PolicyIdAssetRuleOptions> options) : TransactionRuleBase
{
    private readonly PolicyIdAssetRuleOptions _options = options.Value;
    private readonly HashSet<string> _policyIds = new(options.Value.PolicyIds, StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _assetNames = new(options.Value.AssetNames, StringComparer.OrdinalIgnoreCase);

    public override string Id => "policy-id-asset";
    public override string Name => "Policy ID / Asset Match";
    public override string Description => "Matches transactions containing specific policy IDs or asset names";
    public override bool IsEnabled => _options.Enabled && (_policyIds.Count > 0 || _assetNames.Count > 0);

    public override bool IsMatch(TransactionData transaction, RuleContext context) =>
        transaction.MintedAssets.Keys.Any(pid => _policyIds.Contains(pid)) ||
        transaction.MintedAssets.Values.Any(assets => assets.Keys.Any(name => _assetNames.Contains(name)));

    public override RuleMatchResult Evaluate(TransactionData transaction, RuleContext context)
    {
        var matchedPolicies = transaction.MintedAssets.Keys.Where(pid => _policyIds.Contains(pid)).ToList();
        var matchedAssets = transaction.MintedAssets
            .SelectMany(kv => kv.Value.Keys
                .Where(name => _assetNames.Contains(name))
                .Select(name => $"{kv.Key}.{name}"))
            .ToList();

        var criteria = new Dictionary<string, object>();
        if (matchedPolicies.Count > 0) criteria["matched_policies"] = matchedPolicies;
        if (matchedAssets.Count > 0) criteria["matched_assets"] = matchedAssets;

        return new RuleMatchResult(Id, Name, criteria);
    }
}
