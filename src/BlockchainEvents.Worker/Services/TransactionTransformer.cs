namespace BlockchainEvents.Worker.Services;

public static class TransactionTransformer
{
    public static TransactionData TransformPraosTransaction(
        Transaction tx, string blockHash, long blockHeight, long slot, int index) =>
        TransactionDataBuilder
            .For(tx, blockHash, blockHeight, slot, index)
            .WithAllPraosData()
            .Build();

    public static TransactionData TransformBftTransaction(
        Transaction tx, string blockHash, long blockHeight, long slot, int index) =>
        TransactionDataBuilder
            .For(tx, blockHash, blockHeight, slot, index)
            .WithByronData()
            .Build();
}
