namespace BlockchainEvents.Worker.Services;

/// <summary>
/// Emits synthetic CloudEvents for local UI demos when DEMO_EVENTS=true.
/// Includes Minswap V2 outgoing swap visuals for address-match events.
/// </summary>
public sealed class DemoEventSeeder(
    IEventBroadcaster broadcaster,
    DaprClient daprClient,
    IEventMetrics metrics,
    IOptions<BlockchainEventsOptions> options,
    ILogger<DemoEventSeeder> logger) : BackgroundService
{
    private static readonly (string In, string Out, string Dir, string AmtIn, string MinOut, string Fee)[] SwapSamples =
    [
        ("ADA", "MIN", "BUY", "250.5", "18420.12", "2"),
        ("ADA", "SNEK", "BUY", "75", "912340", "2"),
        ("MIN", "ADA", "SELL", "5000", "68.42", "2"),
        ("SNEK", "ADA", "SELL", "250000", "19.8", "2"),
        ("ADA", "WMTX", "BUY", "1200", "845.2", "2"),
        ("NIGHT", "USDCx", "SWAP", "10000", "42.15", "2"),
        ("ADA", "HOSKY", "BUY", "15.25", "8.4M", "2"),
        ("IAG", "ADA", "SELL", "3200", "11.05", "2"),
    ];

    private long _height = 12_450_100;
    private long _slot = 181_200_000;
    private readonly Random _rng = new(42);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogWarning("DEMO_EVENTS=true — seeding synthetic Minswap / blockchain events for UI preview");

        await Task.Delay(1500, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            _height++;
            _slot += _rng.Next(15, 25);
            var blockHash = Guid.NewGuid().ToString("N");
            // Bias: most events in a block are Minswap outgoing swaps.
            // ~1 in 5 blocks is a "heavy haul" — many same-direction swaps for a big lorry in the UI.
            var heavyHaul = _rng.NextDouble() < 0.22;
            var txCount = heavyHaul ? _rng.Next(4, 7) : _rng.Next(3, 8);
            var heavyDir = _rng.NextDouble() < 0.5 ? "BUY" : "SELL";

            var context = new RuleContext(
                Slot: _slot,
                BlockHash: blockHash,
                BlockHeight: _height,
                Era: "Conway",
                Network: options.Value.Network,
                BlockTime: DateTimeOffset.UtcNow);

            for (var i = 0; i < txCount; i++)
            {
                BlockchainEvent<TransactionMatchedData> cloudEvent;
                if (heavyHaul)
                {
                    cloudEvent = CreateMinswapSwap(context, i, heavyDir);
                }
                else
                {
                    var roll = _rng.NextDouble();
                    cloudEvent =
                        roll < 0.62 ? CreateMinswapSwap(context, i) :
                        roll < 0.80 ? CreateMetadata(context, i) :
                        roll < 0.90 ? CreateGovernance(context, i) :
                                      CreateAllTx(context, i);
                }

                var sw = metrics.StartTimer();
                var ruleName = cloudEvent.Data.RuleName;

                // Publish through Dapr so Redis Streams + /events/blockchain delivery
                // (and /subscriptions/status) light up during DEMO_EVENTS demos.
                try
                {
                    await daprClient.PublishEventAsync(
                        options.Value.PubSubName,
                        options.Value.TopicName,
                        cloudEvent,
                        stoppingToken);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Demo Dapr publish failed for {Rule}; continuing with in-process broadcast", ruleName);
                }

                await broadcaster.BroadcastAsync(cloudEvent, stoppingToken);
                metrics.RecordEventEmitted(ruleName);
                metrics.RecordProcessingLatency(sw.Elapsed.TotalMilliseconds, ruleName);
                EventMetrics.CompleteProcessing();
            }

            metrics.RecordBlockProcessed(_slot, txCount);
            logger.LogInformation("Demo block {Height} emitted ({TxCount} matched events)", _height, txCount);
            await Task.Delay(_rng.Next(1200, 2200), stoppingToken);
        }
    }

    private BlockchainEvent<TransactionMatchedData> CreateMinswapSwap(RuleContext context, int index, string? forceDirection = null)
    {
        var candidates = forceDirection is null
            ? SwapSamples
            : SwapSamples.Where(s => s.Dir == forceDirection).ToArray();
        if (candidates.Length == 0) candidates = SwapSamples;

        var sample = candidates[_rng.Next(candidates.Length)];
        var orderType = _rng.NextDouble() > 0.85 ? "StopLoss" : "SwapExactIn";
        var swap = new MinswapSwapDetails
        {
            Dex = "Minswap V2",
            Direction = sample.Dir,
            OrderType = orderType,
            SwapInTicker = sample.In,
            SwapOutTicker = sample.Out,
            SwapInSubject = sample.In == "ADA" ? "" : $"policy{sample.In.ToLowerInvariant()}",
            SwapOutSubject = sample.Out == "ADA" ? "" : $"policy{sample.Out.ToLowerInvariant()}",
            AmountInRaw = "0",
            MinReceiveRaw = "0",
            AmountInDisplay = sample.AmtIn,
            MinReceiveDisplay = sample.MinOut,
            SwapInDecimals = 6,
            SwapOutDecimals = 6,
            BatcherFeeAda = sample.Fee,
            LpTokenSubject = $"lp{sample.In}{sample.Out}",
            DatumSource = "inline"
        };

        var criteria = new Dictionary<string, object>
        {
            ["matched_prefixes"] = new[] { "addr1z8p79rpkcdz8x9d6tft0x0dx5mwuzac2sa4gm8cvkw5hc*" },
            ["swap_summary"] = $"{swap.Direction} {swap.AmountInDisplay} {swap.SwapInTicker} → {swap.MinReceiveDisplay} {swap.SwapOutTicker}",
            ["minswap_swap"] = new Dictionary<string, object?>
            {
                ["dex"] = swap.Dex,
                ["direction"] = swap.Direction,
                ["orderType"] = swap.OrderType,
                ["swapInTicker"] = swap.SwapInTicker,
                ["swapOutTicker"] = swap.SwapOutTicker,
                ["amountIn"] = swap.AmountInDisplay,
                ["minReceive"] = swap.MinReceiveDisplay,
                ["batcherFeeAda"] = swap.BatcherFeeAda,
                ["datumSource"] = swap.DatumSource
            }
        };

        var tx = new TransactionData
        {
            Id = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N")[..32],
            Slot = context.Slot,
            BlockHash = context.BlockHash,
            BlockHeight = context.BlockHeight,
            Index = index,
            Fee = _rng.Next(150_000, 2_500_000),
            InputAddresses = [$"addr1q{_rng.Next(1000, 9999):x}demo{_rng.Next(100, 999):x}"],
            OutputAddresses =
            [
                $"addr1z8p79rpkcdz8x9d6tft0x0dx5mwuzac2sa4gm8cvkw5hc{_rng.Next(10, 99):x}",
                $"addr1q{_rng.Next(1000, 9999):x}recv{_rng.Next(100, 999):x}"
            ],
            MinswapSwap = swap
        };

        return BlockchainEventFactory.Create(new RuleMatchResult("address-match", "Address Match", criteria), tx, context);
    }

    private BlockchainEvent<TransactionMatchedData> CreateMetadata(RuleContext context, int index)
    {
        var criteria = new Dictionary<string, object>
        {
            ["matched_labels"] = new[] { 674 },
            ["match_mode"] = "any_metadata"
        };
        return BlockchainEventFactory.Create(
            new RuleMatchResult("metadata-key-value", "Metadata Key/Value Match", criteria),
            BaseTx(context, index), context);
    }

    private BlockchainEvent<TransactionMatchedData> CreateGovernance(RuleContext context, int index)
    {
        var criteria = new Dictionary<string, object> { ["governance_types"] = new[] { "vote" } };
        return BlockchainEventFactory.Create(
            new RuleMatchResult("governance-treasury", "Governance & Treasury", criteria),
            BaseTx(context, index), context);
    }

    private BlockchainEvent<TransactionMatchedData> CreateAllTx(RuleContext context, int index)
    {
        var criteria = new Dictionary<string, object> { ["match_mode"] = "all" };
        return BlockchainEventFactory.Create(
            new RuleMatchResult("all-transactions", "All Transactions", criteria),
            BaseTx(context, index), context);
    }

    private TransactionData BaseTx(RuleContext context, int index) => new()
    {
        Id = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N")[..32],
        Slot = context.Slot,
        BlockHash = context.BlockHash,
        BlockHeight = context.BlockHeight,
        Index = index,
        Fee = _rng.Next(150_000, 2_500_000),
        InputAddresses = [$"addr1q{_rng.Next(1000, 9999):x}demo{_rng.Next(100, 999):x}"],
        OutputAddresses = [$"addr1q{_rng.Next(1000, 9999):x}out{_rng.Next(100, 999):x}"]
    };
}
