namespace BlockchainEvents.Worker.Services.Extractors;

public interface ITransactionExtractor<out T>
{
    T Extract(Transaction tx);
}
