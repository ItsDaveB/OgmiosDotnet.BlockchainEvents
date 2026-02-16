namespace BlockchainEvents.Engine.Rules;

public sealed class AddressMatchRuleOptions
{
    public const string SectionName = "Rules:AddressMatch";
    public bool Enabled { get; set; } = true;
    public List<string> Addresses { get; set; } = [];
    public List<string> Prefixes { get; set; } = [];
}

public sealed class AddressMatchRule(IOptions<AddressMatchRuleOptions> options) : TransactionRuleBase
{
    private readonly AddressMatchRuleOptions _options = options.Value;
    private readonly HashSet<string> _addresses = new(options.Value.Addresses, StringComparer.OrdinalIgnoreCase);

    public override string Id => "address-match";
    public override string Name => "Address Match";
    public override string Description => "Matches transactions involving specific addresses or prefixes";
    public override bool IsEnabled => _options.Enabled && (_addresses.Count > 0 || _options.Prefixes.Count > 0);

    public override bool IsMatch(TransactionData transaction, RuleContext context) =>
        transaction.AllAddresses.Any(addr =>
            _addresses.Contains(addr) ||
            _options.Prefixes.Any(p => addr.StartsWith(p, StringComparison.OrdinalIgnoreCase)));

    public override RuleMatchResult Evaluate(TransactionData transaction, RuleContext context)
    {
        var matchedAddresses = transaction.AllAddresses.Where(a => _addresses.Contains(a)).ToList();
        var matchedPrefixes = transaction.AllAddresses
            .SelectMany(addr => _options.Prefixes
                .Where(p => addr.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                .Select(p => $"{p}* -> {addr}"))
            .ToList();

        var criteria = new Dictionary<string, object>();
        if (matchedAddresses.Count > 0) criteria["matched_addresses"] = matchedAddresses;
        if (matchedPrefixes.Count > 0) criteria["matched_prefixes"] = matchedPrefixes;

        return new RuleMatchResult(Id, Name, criteria);
    }
}
