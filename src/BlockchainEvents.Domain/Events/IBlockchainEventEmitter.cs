namespace BlockchainEvents.Domain.Events;

public interface IBlockchainEventEmitter
{
    Task EmitAsync(
        TransactionData transaction,
        RuleMatchResult matchResult,
        RuleContext context,
        CancellationToken cancellationToken = default);
}
