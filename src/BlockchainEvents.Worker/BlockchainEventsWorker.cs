namespace BlockchainEvents.Worker;

public sealed class BlockchainEventsWorker(
    IChainSyncService chainSync,
    IRuleEngine ruleEngine,
    IBlockchainEventEmitter eventEmitter,
    ICheckpointService checkpointService,
    IEventMetrics metrics,
    IOptions<BlockchainEventsOptions> options,
    ILogger<BlockchainEventsWorker> logger) : BackgroundService, IChainEventHandler
{
    private SyncCheckpoint? _checkpoint;
    private long _transactionsProcessed;
    private long _eventsEmitted;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var skipDapr = Environment.GetEnvironmentVariable("SKIP_DAPR") == "true";

        if (!skipDapr)
            await WaitForDaprSidecarAsync(stoppingToken);

        logger.LogInformation("Starting chain sync - Network: {Network}, Enabled rules: {Rules}",
            options.Value.Network, string.Join(", ", ruleEngine.EnabledRuleNames));

        metrics.SetEnabledRules(ruleEngine.EnabledRuleCount, ruleEngine.EnabledRuleNames);

        try
        {
            if (!skipDapr)
            {
                _checkpoint = await checkpointService.GetCheckpointAsync(stoppingToken);
                LogCheckpointStatus();
                await chainSync.ResumeAsync(_checkpoint?.Slot, _checkpoint?.BlockHash, stoppingToken);
            }

            await chainSync.StartAsync(this, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Stopping - Processed: {Tx} transactions, {Events} events",
                _transactionsProcessed, _eventsEmitted);
        }
    }

    public async Task OnBlockReceivedAsync(IBlockData block, ITipData tip, CancellationToken ct = default)
    {
        logger.LogDebug("Processing block at slot {Slot}, height {Height}", block.Slot, block.BlockHeight);

        metrics.RecordBlockProcessed(block.Slot, block.Transactions.Count);

        var context = CreateRuleContext(block);
        await ProcessTransactionsAsync(block, context, ct);
        await SaveCheckpointIfNeededAsync(block, ct);
    }

    public async Task OnRollbackAsync(IRollbackPoint point, ITipData tip, CancellationToken ct = default)
    {
        if (point.IsOrigin)
        {
            logger.LogWarning("Rollback to origin");
            await checkpointService.DeleteCheckpointAsync(ct);
            ResetCounters();
        }
        else if (point.Slot.HasValue)
        {
            logger.LogWarning("Rollback to slot {Slot}", point.Slot);
            await SaveRollbackCheckpointAsync(point, ct);
        }
    }

    private async Task ProcessTransactionsAsync(IBlockData block, RuleContext context, CancellationToken ct)
    {
        if (block.Transactions.Count == 0) return;

        var matches = ruleEngine.MatchTransactions(block.Transactions, context).ToList();
        _transactionsProcessed += block.Transactions.Count;

        if (matches.Count == 0) return;

        logger.LogInformation("Block {Slot}: {TxCount} tx, {MatchCount} rule matches",
            block.Slot, block.Transactions.Count, matches.Count);

        foreach (var match in matches)
        {
            logger.LogInformation("[{RuleId}] {RuleName} matched tx {TxId} at slot {Slot}",
                match.MatchResult.RuleId, match.MatchResult.RuleName,
                match.Transaction.Id[..16], block.Slot);

            await eventEmitter.EmitAsync(match.Transaction, match.MatchResult, context, ct);
            _eventsEmitted++;
        }
    }

    private async Task SaveCheckpointIfNeededAsync(IBlockData block, CancellationToken ct)
    {
        var shouldSave = block.BlockHeight % 100 == 0 || _eventsEmitted > (_checkpoint?.EventsEmitted ?? 0);
        if (!shouldSave) return;

        _checkpoint = new SyncCheckpoint(block.Slot, block.BlockHash, block.BlockHeight)
        {
            TransactionsProcessed = _transactionsProcessed,
            EventsEmitted = _eventsEmitted
        };
        await checkpointService.SaveCheckpointAsync(_checkpoint, ct);
    }

    private async Task SaveRollbackCheckpointAsync(IRollbackPoint point, CancellationToken ct)
    {
        _checkpoint = new SyncCheckpoint(point.Slot!.Value, point.BlockHash ?? "unknown", 0)
        {
            TransactionsProcessed = _transactionsProcessed,
            EventsEmitted = _eventsEmitted
        };
        await checkpointService.SaveCheckpointAsync(_checkpoint, ct);
    }

    private RuleContext CreateRuleContext(IBlockData block) =>
        new(block.Slot, block.BlockHash, block.BlockHeight, block.Era, options.Value.Network, DateTimeOffset.UtcNow);

    private void LogCheckpointStatus()
    {
        if (_checkpoint is not null)
            logger.LogInformation("Resuming from slot {Slot}, height {Height}", _checkpoint.Slot, _checkpoint.BlockHeight);
        else
            logger.LogInformation("No checkpoint found, starting fresh");
    }

    private void ResetCounters()
    {
        _checkpoint = null;
        _transactionsProcessed = 0;
        _eventsEmitted = 0;
    }

    private async Task WaitForDaprSidecarAsync(CancellationToken ct)
    {
        logger.LogInformation("Waiting for Dapr sidecar...");
        using var http = new HttpClient();

        for (var i = 0; i < 30; i++)
        {
            try
            {
                var response = await http.GetAsync("http://localhost:3500/v1.0/healthz", ct);
                if (response.IsSuccessStatusCode)
                {
                    logger.LogInformation("Dapr sidecar ready");
                    return;
                }
            }
            catch (HttpRequestException) { }

            await Task.Delay(1000, ct);
        }

        throw new InvalidOperationException("Dapr sidecar did not become ready");
    }
}
