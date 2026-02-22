namespace BlockchainEvents.Worker.Services;

public sealed class OgmiosChainSyncAdapter(
    IServiceProvider serviceProvider,
    IOptions<OgmiosOptions> ogmiosOptions,
    ILogger<OgmiosChainSyncAdapter> logger) : IChainSyncService, IChainSynchronizationMessageHandlers
{
    private readonly OgmiosOptions _config = ogmiosOptions.Value;
    private IChainEventHandler? _handler;
    private long? _resumeSlot;
    private string? _resumeBlockHash;

    public Task ResumeAsync(long? fromSlot, string? fromBlockHash, CancellationToken ct = default)
    {
        _resumeSlot = fromSlot;
        _resumeBlockHash = fromBlockHash;
        return Task.CompletedTask;
    }

    public async Task StartAsync(IChainEventHandler handler, CancellationToken ct = default)
    {
        _handler = handler;

        var startingPoint = DetermineStartingPoint();

        logger.LogInformation("Starting chain sync from slot {Slot}", startingPoint.StartingPointSlot ?? 0);

        var chainSync = serviceProvider.GetRequiredService<IChainSynchronizationClientService>();
        var contextService = serviceProvider.GetRequiredService<IInteractionContextService>();

        var context = await contextService.CreateInteractionContextAsync(
            "chain-sync", startingPoint, _config.Connection);
        await chainSync.ResumeAsync([context], 100);
    }

    public async Task RollForwardHandler(Block block, string blockType, Tip tip)
    {
        if (_handler is null) return;
        await _handler.OnBlockReceivedAsync(TransformBlock(block, blockType), TransformTip(tip));
    }

    public async Task RollBackwardHandler(Generated.Ogmios.PointOrOrigin point, Generated.Ogmios.TipOrOrigin tip)
    {
        if (_handler is null) return;
        await _handler.OnRollbackAsync(TransformRollbackPoint(point), TransformTip(tip));
    }

    private StartingPointConfiguration DetermineStartingPoint()
    {
        var hasValidCheckpoint = _resumeSlot > 0 &&
                                  !string.IsNullOrEmpty(_resumeBlockHash) &&
                                  _resumeBlockHash != "unknown";

        if (hasValidCheckpoint)
            return new StartingPointConfiguration
            {
                StartingPointIdOrOrigin = _resumeBlockHash!,
                StartingPointSlot = _resumeSlot
            };

        var configuredStart = _config.StartingPoints?.FirstOrDefault();
        if (configuredStart is not null)
        {
            logger.LogInformation("No checkpoint found, using configured starting point at slot {Slot}", configuredStart.StartingPointSlot);
            return configuredStart;
        }

        logger.LogInformation("No checkpoint or configured starting point found, starting from origin");
        return new StartingPointConfiguration { StartingPointIdOrOrigin = "origin" };
    }

    private OgmiosBlockData TransformBlock(Block block, string blockType) =>
        blockType.ToLowerInvariant() switch
        {
            "praos" => TransformPraosBlock(block),
            "bft" => TransformBftBlock(block),
            "ebb" => TransformEbbBlock(block),
            _ => new OgmiosBlockData(0, "unknown", 0, "unknown", blockType, [])
        };

    private OgmiosBlockData TransformPraosBlock(Block block)
    {
        var praos = block.AsBlockPraos;
        var hash = (string)praos.Id.AsString;
        var era = (string)praos.Era.AsString;
        var transactions = praos.Transactions
            .Select((tx, i) => TransactionTransformer.TransformPraosTransaction(tx, hash, (long)praos.Height, (long)praos.Slot, i))
            .ToList();
        return new OgmiosBlockData((long)praos.Slot, hash, (long)praos.Height, era, "praos", transactions);
    }

    private OgmiosBlockData TransformBftBlock(Block block)
    {
        var bft = block.AsBlockBft;
        var hash = (string)bft.Id.AsString;
        var transactions = bft.Transactions
            .Select((tx, i) => TransactionTransformer.TransformBftTransaction(tx, hash, (long)bft.Height, (long)bft.Slot, i))
            .ToList();
        return new OgmiosBlockData((long)bft.Slot, hash, (long)bft.Height, "byron", "bft", transactions);
    }

    private OgmiosBlockData TransformEbbBlock(Block block)
    {
        var ebb = block.AsBlockEbb;
        var hash = (string)ebb.Id.AsString;
        return new OgmiosBlockData(0, hash, (long)ebb.Height, "byron", "ebb", []);
    }

    private static OgmiosTipData TransformTip(Tip tip)
    {
        var hash = (string)tip.Id.AsString;
        return new OgmiosTipData((long)tip.Slot, hash, (long)tip.Height);
    }

    private static OgmiosTipData TransformTip(Generated.Ogmios.TipOrOrigin tip)
    {
        if (tip.IsOrigin) return new OgmiosTipData(0, "origin", 0);

        var t = tip.AsTip;
        return new OgmiosTipData((long)t.Slot, (string)t.Id.AsString, (long)t.Height);
    }

    private static OgmiosRollbackPoint TransformRollbackPoint(Generated.Ogmios.PointOrOrigin point)
    {
        if (point.IsOrigin) return new OgmiosRollbackPoint(true, null, null);

        var p = point.AsPoint;
        return new OgmiosRollbackPoint(false, (long)p.Slot, (string)p.Id.AsString);
    }
}

internal sealed record OgmiosBlockData(
    long Slot, string BlockHash, long BlockHeight, string Era, string BlockType,
    IReadOnlyList<TransactionData> Transactions) : IBlockData;

internal sealed record OgmiosTipData(long Slot, string BlockHash, long BlockHeight) : ITipData;

internal sealed record OgmiosRollbackPoint(bool IsOrigin, long? Slot, string? BlockHash) : IRollbackPoint;
