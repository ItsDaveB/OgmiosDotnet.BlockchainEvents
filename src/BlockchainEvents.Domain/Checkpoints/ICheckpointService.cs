namespace BlockchainEvents.Domain.Checkpoints;

public interface ICheckpointService
{
    Task<SyncCheckpoint?> GetCheckpointAsync(CancellationToken cancellationToken = default);
    Task SaveCheckpointAsync(SyncCheckpoint checkpoint, CancellationToken cancellationToken = default);
    Task DeleteCheckpointAsync(CancellationToken cancellationToken = default);
}
