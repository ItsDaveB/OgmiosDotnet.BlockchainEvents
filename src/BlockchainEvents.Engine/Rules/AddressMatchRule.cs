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
    private readonly HashSet<string> _addresses = new(options.Value.Addresses, StringComparer.OrdinalIgnoreCase);

    public override string Id => "address-match";
    public override string Name => "Address Match";
    public override string Description => "Matches transactions involving specific addresses or prefixes";
    public override bool IsEnabled => options.Value.Enabled && (_addresses.Count > 0 || options.Value.Prefixes.Count > 0);

    public override bool IsMatch(TransactionData transaction, RuleContext context) =>
        transaction.AllAddresses.Any(addr =>
            _addresses.Contains(addr) ||
            options.Value.Prefixes.Any(p => addr.StartsWith(p, StringComparison.OrdinalIgnoreCase)));

    public override RuleMatchResult Evaluate(TransactionData transaction, RuleContext context)
    {
        var matchedAddresses = transaction.AllAddresses.Where(a => _addresses.Contains(a)).ToList();
        var matchedPrefixes = transaction.AllAddresses
            .SelectMany(addr => options.Value.Prefixes
                .Where(p => addr.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                .Select(p => $"{p}* -> {addr}"))
            .ToList();

        var criteria = new Dictionary<string, object>();
        if (matchedAddresses.Count > 0) criteria["matched_addresses"] = matchedAddresses;
        if (matchedPrefixes.Count > 0) criteria["matched_prefixes"] = matchedPrefixes;

        if (transaction.MinswapSwap is { } swap)
        {
            criteria["minswap_swap"] = new Dictionary<string, object?>
            {
                ["dex"] = swap.Dex,
                ["direction"] = swap.Direction,
                ["orderType"] = swap.OrderType,
                ["swapInTicker"] = swap.SwapInTicker,
                ["swapOutTicker"] = swap.SwapOutTicker,
                ["swapInSubject"] = swap.SwapInSubject,
                ["swapOutSubject"] = swap.SwapOutSubject,
                ["amountIn"] = swap.AmountInDisplay,
                ["amountInRaw"] = swap.AmountInRaw,
                ["minReceive"] = swap.MinReceiveDisplay,
                ["minReceiveRaw"] = swap.MinReceiveRaw,
                ["batcherFeeAda"] = swap.BatcherFeeAda,
                ["lpTokenSubject"] = swap.LpTokenSubject,
                ["datumSource"] = swap.DatumSource
            };
            criteria["swap_summary"] =
                $"{swap.Direction} {swap.AmountInDisplay} {swap.SwapInTicker} → {swap.MinReceiveDisplay} {swap.SwapOutTicker}";
        }

        return new RuleMatchResult(Id, Name, criteria);
    }
}
