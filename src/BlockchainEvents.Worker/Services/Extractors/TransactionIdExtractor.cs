namespace BlockchainEvents.Worker.Services.Extractors;

public sealed class TransactionIdExtractor
{
    public static readonly TransactionIdExtractor Instance = new();

    public string Extract(Transaction tx, long slot, int index) =>
        tx.Id.IsNotNullOrUndefined()
            ? (string)tx.Id.AsString
            : $"tx-{slot}-{index}";

    public long ExtractFee(Transaction tx) =>
        tx.Fee.IsNotNullOrUndefined()
            ? (long)tx.Fee.Ada.Lovelace
            : 0L;
}
