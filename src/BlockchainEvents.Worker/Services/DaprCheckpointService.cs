namespace BlockchainEvents.Worker.Services;

public sealed class DaprCheckpointService(
    DaprClient daprClient,
    IOptions<BlockchainEventsOptions> options,
    ILogger<DaprCheckpointService> logger) : ICheckpointService
{
    private readonly BlockchainEventsOptions _options = options.Value;
    private string? _currentEtag;

    /// <inheritdoc />
    public async Task<SyncCheckpoint?> GetCheckpointAsync(CancellationToken cancellationToken = default)
    {
        var (checkpoint, etag) = await daprClient.GetStateAndETagAsync<SyncCheckpoint>(
            _options.StateStoreName,
            _options.CheckpointKey,
            cancellationToken: cancellationToken);

        if (checkpoint is not null)
        {
            _currentEtag = etag;
            logger.LogInformation(
                "Retrieved checkpoint: Slot {Slot}, Block {BlockHash}, Height {Height}",
                checkpoint.Slot, checkpoint.BlockHash, checkpoint.BlockHeight);
        }
        else
        {
            logger.LogInformation("No checkpoint found, starting fresh");
        }

        return checkpoint;
    }

    /// <inheritdoc />
    public async Task SaveCheckpointAsync(SyncCheckpoint checkpoint, CancellationToken cancellationToken = default)
    {
        if (_currentEtag is not null)
        {
            var success = await daprClient.TrySaveStateAsync(
                _options.StateStoreName,
                _options.CheckpointKey,
                checkpoint,
                _currentEtag,
                cancellationToken: cancellationToken);

            if (!success)
            {
                throw new InvalidOperationException(
                    $"Failed to save checkpoint due to concurrent modification. Expected ETag: {_currentEtag}");
            }
        }
        else
        {
            await daprClient.SaveStateAsync(
                _options.StateStoreName,
                _options.CheckpointKey,
                checkpoint,
                cancellationToken: cancellationToken);
        }

        var (_, newEtag) = await daprClient.GetStateAndETagAsync<SyncCheckpoint>(
            _options.StateStoreName,
            _options.CheckpointKey,
            cancellationToken: cancellationToken);
        _currentEtag = newEtag;

        logger.LogDebug(
            "Saved checkpoint: Slot {Slot}, Height {Height}",
            checkpoint.Slot, checkpoint.BlockHeight);
    }

    /// <inheritdoc />
    public async Task DeleteCheckpointAsync(CancellationToken cancellationToken = default)
    {
        await daprClient.DeleteStateAsync(
            _options.StateStoreName,
            _options.CheckpointKey,
            cancellationToken: cancellationToken);

        _currentEtag = null;
        logger.LogInformation("Deleted checkpoint");
    }
}
