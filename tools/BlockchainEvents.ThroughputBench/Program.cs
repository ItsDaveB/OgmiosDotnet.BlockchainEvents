using System.Diagnostics;
using BlockchainEvents.Domain.Rules;
using BlockchainEvents.Engine;
using BlockchainEvents.Engine.Rules;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

// In-process rule-engine stress bench (no Docker / Dapr / Ogmios).
// Usage:
//   dotnet run -c Release --project tools/BlockchainEvents.ThroughputBench -- [seconds] [parallelism]

var seconds = args.Length > 0 && int.TryParse(args[0], out var s) ? Math.Clamp(s, 1, 120) : 10;
var parallelism = args.Length > 1 && int.TryParse(args[1], out var p)
    ? Math.Clamp(p, 1, Environment.ProcessorCount * 2)
    : Environment.ProcessorCount;

var engine = CreateEngine();
var context = new RuleContext(
    Slot: 181_200_000,
    BlockHash: "bench",
    BlockHeight: 12_450_000,
    Era: "Conway",
    Network: "mainnet",
    BlockTime: DateTimeOffset.UtcNow);

var pool = BuildTransactionPool(50_000);
Console.WriteLine("OgmiosDotnet.BlockchainEvents — rule engine throughput bench");
Console.WriteLine($"  Rules enabled : {engine.EnabledRuleCount} ({string.Join(", ", engine.EnabledRuleNames)})");
Console.WriteLine($"  Pool size     : {pool.Length:N0} synthetic txs");
Console.WriteLine($"  Duration      : {seconds}s");
Console.WriteLine($"  Parallelism   : {parallelism} (CPU={Environment.ProcessorCount})");
Console.WriteLine();

for (var i = 0; i < 5_000; i++)
    _ = engine.MatchTransaction(pool[i % pool.Length], context).Count();

var txs = 0L;
var matches = 0L;
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(seconds));
var sw = Stopwatch.StartNew();

var tasks = Enumerable.Range(0, parallelism).Select(_ => Task.Run(() =>
{
    var localTxs = 0L;
    var localMatches = 0L;
    var i = Random.Shared.Next(pool.Length);
    while (!cts.IsCancellationRequested)
    {
        var tx = pool[i++ % pool.Length];
        var hit = 0;
        foreach (var _ in engine.MatchTransaction(tx, context))
            hit++;
        localTxs++;
        localMatches += hit;
    }
    Interlocked.Add(ref txs, localTxs);
    Interlocked.Add(ref matches, localMatches);
})).ToArray();

await Task.WhenAll(tasks);
sw.Stop();

var elapsed = Math.Max(sw.Elapsed.TotalSeconds, 0.001);
Console.WriteLine("Results (in-process Engine only — no network / Redis / Dapr)");
Console.WriteLine($"  Elapsed               : {elapsed:F2}s");
Console.WriteLine($"  Transactions evaluated: {txs:N0}");
Console.WriteLine($"  Rule matches emitted  : {matches:N0}");
Console.WriteLine($"  Throughput            : {txs / elapsed:N0} tx/sec");
Console.WriteLine($"  Match throughput      : {matches / elapsed:N0} matches/sec");
Console.WriteLine();
Console.WriteLine("End-to-end mainnet catch-up (docs/benchmarks.md):");
Console.WriteLine("  ~207 tx/sec · ~284 events/sec · p99 1–5 ms · 0% errors");

static RuleEngine CreateEngine()
{
    ITransactionRule[] rules =
    [
        new AddressMatchRule(Options.Create(new AddressMatchRuleOptions
        {
            Enabled = true,
            Prefixes = ["addr1z8p79rpkcdz8x9d6tft0x0dx5mwuzac2sa4gm8cvkw5hc"]
        })),
        new MetadataKeyValueRule(Options.Create(new MetadataKeyValueRuleOptions
        {
            Enabled = true,
            Labels = [674]
        })),
        new GovernanceTreasuryRule(Options.Create(new GovernanceTreasuryRuleOptions
        {
            Enabled = true,
            IncludeVotes = true,
            IncludeGovernanceActions = true,
            IncludeTreasuryWithdrawals = true
        })),
        new AllTransactionsRule(Options.Create(new AllTransactionsRuleOptions { Enabled = true })),
    ];

    return new RuleEngine(rules, NullLogger<RuleEngine>.Instance);
}

static TransactionData[] BuildTransactionPool(int count)
{
    var rng = new Random(42);
    var pool = new TransactionData[count];
    for (var i = 0; i < count; i++)
    {
        var roll = rng.NextDouble();
        var outputs = new List<string>
        {
            $"addr1q{rng.Next(1000, 9999):x}recv{rng.Next(100, 999):x}"
        };
        if (roll < 0.15)
            outputs.Add($"addr1z8p79rpkcdz8x9d6tft0x0dx5mwuzac2sa4gm8cvkw5hc{rng.Next(10, 99):x}");

        IReadOnlyDictionary<int, object?> metadata = roll is >= 0.15 and < 0.40
            ? new Dictionary<int, object?> { [674] = new Dictionary<string, object> { ["msg"] = "bench" } }
            : new Dictionary<int, object?>();

        pool[i] = new TransactionData
        {
            Id = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N")[..32],
            Slot = 181_200_000 + i,
            BlockHash = "bench",
            BlockHeight = 12_450_000,
            Index = i % 64,
            Fee = rng.Next(150_000, 2_500_000),
            InputAddresses = [$"addr1q{rng.Next(1000, 9999):x}in{rng.Next(100, 999):x}"],
            OutputAddresses = outputs,
            Metadata = metadata,
            HasVote = roll is >= 0.40 and < 0.45,
            HasTreasuryWithdrawal = roll is >= 0.45 and < 0.47,
            HasGovernanceAction = roll is >= 0.47 and < 0.49,
        };
    }
    return pool;
}
