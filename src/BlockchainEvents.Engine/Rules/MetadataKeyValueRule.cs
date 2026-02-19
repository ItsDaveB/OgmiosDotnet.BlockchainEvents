namespace BlockchainEvents.Engine.Rules;

public sealed class MetadataKeyValueRuleOptions
{
    public const string SectionName = "Rules:MetadataKeyValue";
    public bool Enabled { get; set; } = true;
    public List<int> Labels { get; set; } = [];
    public List<string> KeyPatterns { get; set; } = [];
    public List<string> ValuePatterns { get; set; } = [];
}

public sealed class MetadataKeyValueRule(IOptions<MetadataKeyValueRuleOptions> options) : TransactionRuleBase
{
    private readonly MetadataKeyValueRuleOptions _options = options.Value;
    private readonly HashSet<int> _labels = [.. options.Value.Labels];
    private readonly HashSet<string> _keyPatterns = new(options.Value.KeyPatterns, StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _valuePatterns = new(options.Value.ValuePatterns, StringComparer.OrdinalIgnoreCase);

    public override string Id => "metadata-key-value";
    public override string Name => "Metadata Key/Value Match";
    public override string Description => "Matches transactions containing specific metadata labels or patterns";
    public override bool IsEnabled => _options.Enabled && (_labels.Count > 0 || _keyPatterns.Count > 0 || _valuePatterns.Count > 0);

    public override bool IsMatch(TransactionData transaction, RuleContext context)
    {
        if (transaction.Metadata.Count == 0) return false;

        if (_labels.Count > 0 && transaction.Metadata.Keys.Any(label => _labels.Contains(label)))
            return true;

        return (_keyPatterns.Count > 0 || _valuePatterns.Count > 0) &&
               transaction.Metadata.Values.Any(v => SearchMetadata(v, _keyPatterns, _valuePatterns));
    }

    public override RuleMatchResult Evaluate(TransactionData transaction, RuleContext context)
    {
        var matchedLabels = transaction.Metadata.Keys.Where(l => _labels.Contains(l)).ToList();
        var criteria = new Dictionary<string, object>();

        if (matchedLabels.Count > 0) criteria["matched_labels"] = matchedLabels;
        if (_keyPatterns.Count > 0) criteria["key_patterns"] = _keyPatterns.ToList();
        if (_valuePatterns.Count > 0) criteria["value_patterns"] = _valuePatterns.ToList();

        return new RuleMatchResult(Id, Name, criteria);
    }

    private static bool SearchMetadata(object? value, HashSet<string> keyPatterns, HashSet<string> valuePatterns)
    {
        if (value is null) return false;

        if (value is JsonElement element)
        {
            return SearchJsonElement(element, keyPatterns, valuePatterns);
        }

        if (value is IDictionary<string, object> dict)
        {
            foreach (var (key, val) in dict)
            {
                if (keyPatterns.Any(pattern => key.Contains(pattern, StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
                if (SearchMetadata(val, keyPatterns, valuePatterns))
                {
                    return true;
                }
            }
        }

        if (value is string str)
        {
            return valuePatterns.Any(pattern => str.Contains(pattern, StringComparison.OrdinalIgnoreCase));
        }

        if (value is IEnumerable<object> list)
        {
            foreach (var item in list)
            {
                if (SearchMetadata(item, keyPatterns, valuePatterns))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool SearchJsonElement(JsonElement element, HashSet<string> keyPatterns, HashSet<string> valuePatterns)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => valuePatterns.Any(pattern =>
                element.GetString()?.Contains(pattern, StringComparison.OrdinalIgnoreCase) ?? false),
            JsonValueKind.Object => element.EnumerateObject().Any(prop =>
                keyPatterns.Any(pattern => prop.Name.Contains(pattern, StringComparison.OrdinalIgnoreCase)) ||
                SearchJsonElement(prop.Value, keyPatterns, valuePatterns)),
            JsonValueKind.Array => element.EnumerateArray()
                .Any(item => SearchJsonElement(item, keyPatterns, valuePatterns)),
            _ => false
        };
    }
}
