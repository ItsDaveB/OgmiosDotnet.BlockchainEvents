namespace BlockchainEvents.Worker.Services.Extractors;

public sealed class AddressExtractor : ITransactionExtractor<IReadOnlyList<string>>
{
    public static readonly AddressExtractor Instance = new();

    public IReadOnlyList<string> Extract(Transaction tx)
    {
        if (tx.Outputs.ValueKind != JsonValueKind.Array)
            return [];

        return tx.Outputs
            .Where(o => o.Address.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
            .Select(o => (string)o.Address.AsString)
            .ToList();
    }
}
