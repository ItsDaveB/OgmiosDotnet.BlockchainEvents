namespace BlockchainEvents.Worker.Services.Extractors;

public sealed class TransactionIdExtractor
{
    public static readonly TransactionIdExtractor Instance = new();

    public string Extract(Transaction tx, long slot, int index) =>
        tx.Id.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined
            ? (string)tx.Id.AsString
            : $"tx-{slot}-{index}";

    public long ExtractFee(Transaction tx) =>
        tx.Fee.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined
            ? (long)tx.Fee.Ada.Lovelace
            : 0L;
}
