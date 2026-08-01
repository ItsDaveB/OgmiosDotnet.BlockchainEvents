namespace BlockchainEvents.Worker.Services;

/// <summary>
/// Full-stack load generator: synthetic txs → <see cref="IRuleEngine"/> →
/// <see cref="IBlockchainEventEmitter"/> (Dapr + SSE broadcast + Prometheus).
/// Enabled with BENCH_LOAD=true (skips Ogmios). Not for UI demos — use DEMO_EVENTS for that.
/// </summary>
public sealed class BenchLoadSeeder(
    IRuleEngine ruleEngine,
    IBlockchainEventEmitter eventEmitter,
    IEventMetrics metrics,
    IOptions<BlockchainEventsOptions> options,
    ILogger<BenchLoadSeeder> logger) : BackgroundService
{
    private readonly Random _rng = new(42);
    private long _height = 12_500_000;
    private long _slot = 182_000_000;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var txPerBlock = ParseInt("BENCH_TX_PER_BLOCK", 64, 1, 500);
        var delayMs = ParseInt("BENCH_DELAY_MS", 0, 0, 5_000);
        var durationSec = ParseInt("BENCH_DURATION_SEC", 0, 0, 3600); // 0 = run until stop

        logger.LogWarning(
            "BENCH_LOAD=true — full-stack throughput bench (RuleEngine → Dapr). " +
            "tx/block={TxPerBlock}, delayMs={DelayMs}, durationSec={Duration} (0=until stop). Rules: {Rules}",
            txPerBlock, delayMs, durationSec, string.Join(", ", ruleEngine.EnabledRuleNames));

        // Let Dapr sidecar finish init after Redis is healthy.
        await Task.Delay(4_000, stoppingToken);

        var deadline = durationSec > 0
            ? DateTimeOffset.UtcNow.AddSeconds(durationSec)
            : DateTimeOffset.MaxValue;

        while (!stoppingToken.IsCancellationRequested && DateTimeOffset.UtcNow < deadline)
        {
            _height++;
            _slot += 20;
            var blockHash = Guid.NewGuid().ToString("N");
            var txs = BuildBlockTransactions(txPerBlock, _slot, blockHash, _height);

            metrics.RecordBlockProcessed(_slot, txs.Count);

            var context = new RuleContext(
                Slot: _slot,
                BlockHash: blockHash,
                BlockHeight: _height,
                Era: "Conway",
                Network: options.Value.Network,
                BlockTime: DateTimeOffset.UtcNow);

            var matches = ruleEngine.MatchTransactions(txs, context).ToList();
            foreach (var match in matches)
                await eventEmitter.EmitAsync(match.Transaction, match.MatchResult, context, stoppingToken);

            if (delayMs > 0)
                await Task.Delay(delayMs, stoppingToken);
        }

        logger.LogWarning("BENCH_LOAD finished (cancelled or duration elapsed)");
    }

    private List<TransactionData> BuildBlockTransactions(int count, long slot, string blockHash, long height)
    {
        var list = new List<TransactionData>(count);
        for (var i = 0; i < count; i++)
        {
            var roll = _rng.NextDouble();
            var outputs = new List<string> { $"addr1q{_rng.Next(1000, 9999):x}out{_rng.Next(100, 999):x}" };
            if (roll < 0.20)
                outputs.Add($"addr1z8p79rpkcdz8x9d6tft0x0dx5mwuzac2sa4gm8cvkw5hc{_rng.Next(10, 99):x}");

            IReadOnlyDictionary<int, object?> metadata = roll is >= 0.20 and < 0.55
                ? new Dictionary<int, object?> { [674] = new Dictionary<string, object> { ["msg"] = "bench" } }
                : new Dictionary<int, object?>();

            list.Add(new TransactionData
            {
                Id = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N")[..32],
                Slot = slot,
                BlockHash = blockHash,
                BlockHeight = height,
                Index = i,
                Fee = _rng.Next(150_000, 2_500_000),
                InputAddresses = [$"addr1q{_rng.Next(1000, 9999):x}in{_rng.Next(100, 999):x}"],
                OutputAddresses = outputs,
                Metadata = metadata,
                HasVote = roll is >= 0.55 and < 0.62,
                HasTreasuryWithdrawal = roll is >= 0.62 and < 0.65,
                HasGovernanceAction = roll is >= 0.65 and < 0.68,
            });
        }
        return list;
    }

    private static int ParseInt(string env, int fallback, int min, int max)
    {
        if (!int.TryParse(Environment.GetEnvironmentVariable(env), out var v))
            return fallback;
        return Math.Clamp(v, min, max);
    }
}
