namespace BlockchainEvents.Worker.Services.Extractors;

public sealed class MetadataExtractor : ITransactionExtractor<IReadOnlyDictionary<int, object?>>
{
    public static readonly MetadataExtractor Instance = new();

    public IReadOnlyDictionary<int, object?> Extract(Transaction tx)
    {
        if (!tx.Metadata.IsNotNullOrUndefined() ||
            !tx.Metadata.Labels.IsNotNullOrUndefined())
            return new Dictionary<int, object?>();

        var result = new Dictionary<int, object?>();

        foreach (var label in tx.Metadata.Labels)
        {
            if (int.TryParse(label.Key.GetString(), out var key) &&
                label.Value.Json.IsNotNullOrUndefined())
            {
                result[key] = label.Value.Json.ToString();
            }
        }

        return result;
    }
}
