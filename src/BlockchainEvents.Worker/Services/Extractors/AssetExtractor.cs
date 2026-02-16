namespace BlockchainEvents.Worker.Services.Extractors;

public sealed class AssetExtractor : ITransactionExtractor<IReadOnlyDictionary<string, Dictionary<string, long>>>
{
    public static readonly AssetExtractor Instance = new();

    public IReadOnlyDictionary<string, Dictionary<string, long>> Extract(Transaction tx)
    {
        if (tx.Mint.ValueKind != JsonValueKind.Object)
            return new Dictionary<string, Dictionary<string, long>>();

        var result = new Dictionary<string, Dictionary<string, long>>();

        foreach (var policy in tx.Mint)
        {
            if (policy.Value.ValueKind != JsonValueKind.Object) continue;

            var policyId = policy.Key.GetString();
            var assets = new Dictionary<string, long>();

            foreach (var asset in policy.Value.Where(a => a.Value.ValueKind == JsonValueKind.Number))
                assets[asset.Key.GetString()] = (long)asset.Value;

            if (assets.Count > 0)
                result[policyId] = assets;
        }

        return result;
    }
}
