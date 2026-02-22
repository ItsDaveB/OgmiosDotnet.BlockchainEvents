namespace BlockchainEvents.Worker.Services.Extractors;

public sealed class AddressExtractor : ITransactionExtractor<IReadOnlyList<string>>
{
    public static readonly AddressExtractor Instance = new();

    public IReadOnlyList<string> Extract(Transaction tx)
    {
        if (!tx.Outputs.IsNotNullOrUndefined())
            return [];

        return [.. tx.Outputs
            .Where(o => o.Address.IsNotNullOrUndefined())
            .Select(o => (string)o.Address.AsString)];
    }
}
