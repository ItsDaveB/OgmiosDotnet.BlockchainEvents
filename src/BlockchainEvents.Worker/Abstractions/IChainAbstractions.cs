namespace BlockchainEvents.Worker.Abstractions;

public interface IBlockData
{
    long Slot { get; }
    string BlockHash { get; }
    long BlockHeight { get; }
    string Era { get; }
    string BlockType { get; }
    IReadOnlyList<TransactionData> Transactions { get; }
}

public interface ITipData
{
    long Slot { get; }
    string BlockHash { get; }
    long BlockHeight { get; }
}

public interface IRollbackPoint
{
    bool IsOrigin { get; }
    long? Slot { get; }
    string? BlockHash { get; }
}

public interface IChainEventHandler
{
    Task OnBlockReceivedAsync(IBlockData block, ITipData tip, CancellationToken ct = default);
    Task OnRollbackAsync(IRollbackPoint point, ITipData tip, CancellationToken ct = default);
}

public interface IChainSyncService
{
    Task ResumeAsync(long? fromSlot, string? fromBlockHash, CancellationToken ct = default);
    Task StartAsync(IChainEventHandler handler, CancellationToken ct = default);
}
