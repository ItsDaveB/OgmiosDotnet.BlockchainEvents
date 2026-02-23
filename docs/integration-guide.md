# Integration Guide: Building Custom Rules

This guide explains how to create custom transaction filtering rules for OgmiosDotnet.BlockchainEvents.

## Overview

The rule engine uses a pluggable architecture where each rule implements a common interface. Rules are evaluated against every transaction, and matching transactions generate CloudEvents for downstream consumption.

## Rule Interface

All rules implement `ITransactionRule`:

```csharp
public interface ITransactionRule
{
    string Id { get; }
    string Name { get; }
    string Description { get; }
    bool IsEnabled { get; }

    bool IsMatch(TransactionData transaction, RuleContext context);
    RuleMatchResult Evaluate(TransactionData transaction, RuleContext context);
}
```

| Member        | Purpose                                      |
| ------------- | -------------------------------------------- |
| `Id`          | Unique identifier (used in event type)       |
| `Name`        | Human-readable name (used in event subject)  |
| `Description` | What the rule filters for                    |
| `IsEnabled`   | Whether rule is currently active             |
| `IsMatch`     | Fast check if transaction matches            |
| `Evaluate`    | Detailed evaluation returning match criteria |

## Creating a Custom Rule

### Step 1: Define the Rule Class

Create a new file in `src/BlockchainEvents.Engine/Rules/`:

```csharp
using BlockchainEvents.Domain.Rules;
using Microsoft.Extensions.Options;

namespace BlockchainEvents.Engine.Rules;

/// <summary>
/// Matches transactions with fees above a threshold.
/// </summary>
public sealed class HighFeeRule(IOptions<HighFeeRuleOptions> options) : TransactionRuleBase
{
    private readonly HighFeeRuleOptions _options = options.Value;

    public override string Id => "high-fee";
    public override string Name => "High Fee";
    public override string Description => "Matches transactions with fees above threshold";
    public override bool IsEnabled => _options.Enabled && _options.ThresholdLovelace > 0;

    public override bool IsMatch(TransactionData transaction, RuleContext context)
        => transaction.Fee > _options.ThresholdLovelace;

    public override RuleMatchResult Evaluate(TransactionData transaction, RuleContext context)
        => new(Id, Name, new Dictionary<string, object>
        {
            ["fee_lovelace"] = transaction.Fee,
            ["threshold_lovelace"] = _options.ThresholdLovelace,
            ["fee_ada"] = transaction.Fee / 1_000_000m
        });
}
```

### Step 2: Define Configuration Options

Add an options class for configuration:

```csharp
public sealed class HighFeeRuleOptions
{
    public const string SectionName = "Rules:HighFee";
    public bool Enabled { get; set; } = true;
    public long ThresholdLovelace { get; set; } = 5_000_000; // 5 ADA
}
```

### Step 3: Register the Rule

In `Program.cs`, register your rule:

```csharp
// Configuration
builder.Services.Configure<HighFeeRuleOptions>(
    builder.Configuration.GetSection(HighFeeRuleOptions.SectionName));

// Rule registration
builder.Services.AddSingleton<ITransactionRule, HighFeeRule>();
```

### Step 4: Add Configuration

In `appsettings.json`:

```json
{
  "Rules": {
    "HighFee": {
      "Enabled": true,
      "ThresholdLovelace": 5000000
    }
  }
}
```

## TransactionData Reference

The `TransactionData` object contains normalized transaction information:

```csharp
public sealed class TransactionData
{
    public required string Id { get; init; }              // Transaction hash
    public required long Slot { get; init; }              // Slot number
    public required string BlockHash { get; init; }       // Block hash
    public required long BlockHeight { get; init; }       // Block height
    public required int Index { get; init; }              // Index in block
    public long Fee { get; init; }                        // Fee in lovelace

    // Addresses
    public IReadOnlyList<string> InputAddresses { get; init; } = [];
    public IReadOnlyList<string> OutputAddresses { get; init; } = [];
    public IEnumerable<string> AllAddresses { get; }      // Combined, deduplicated

    // Assets
    public IReadOnlyDictionary<string, Dictionary<string, long>> MintedAssets { get; init; }

    // Metadata
    public IReadOnlyDictionary<int, object?> Metadata { get; init; }

    // Governance/Staking flags
    public bool HasGovernanceAction { get; init; }
    public bool HasTreasuryWithdrawal { get; init; }
    public bool HasStakeDelegation { get; init; }
    public bool HasStakeRegistration { get; init; }
    public bool HasVote { get; init; }
}
```

## RuleContext Reference

The `RuleContext` provides block-level information:

```csharp
public sealed record RuleContext(
    long Slot,               // Block slot number
    string BlockHash,        // Block hash
    long BlockHeight,        // Block height
    string Era,              // Era name (Conway, Babbage, etc.)
    string Network,          // Network (mainnet, preprod, preview)
    DateTimeOffset BlockTime // Approximate block time
);
```

## Example Rules

### Whale Transaction Rule

Matches transactions with large ADA movements:

```csharp
public sealed class WhaleTransactionRule(IOptions<WhaleOptions> options) : TransactionRuleBase
{
    private readonly WhaleOptions _options = options.Value;

    public override string Id => "whale-transaction";
    public override string Name => "Whale Transaction";
    public override string Description => "Matches large ADA movements";
    public override bool IsEnabled => _options.Enabled;

    public override bool IsMatch(TransactionData transaction, RuleContext context)
    {
        // This is a simplified check - real implementation would sum outputs
        return transaction.OutputAddresses.Count > 0 && transaction.Fee > 1_000_000;
    }

    public override RuleMatchResult Evaluate(TransactionData transaction, RuleContext context)
        => new(Id, Name, new Dictionary<string, object>
        {
            ["output_count"] = transaction.OutputAddresses.Count,
            ["fee_lovelace"] = transaction.Fee
        });
}
```

### Specific Token Mint Rule

Matches minting of specific tokens:

```csharp
public sealed class TokenMintRule(IOptions<TokenMintOptions> options) : TransactionRuleBase
{
    private readonly TokenMintOptions _options = options.Value;
    private readonly HashSet<string> _watchedPolicies = new(
        options.Value.PolicyIds, StringComparer.OrdinalIgnoreCase);

    public override string Id => "token-mint";
    public override string Name => "Token Mint";
    public override string Description => "Matches specific token minting events";
    public override bool IsEnabled => _options.Enabled && _watchedPolicies.Count > 0;

    public override bool IsMatch(TransactionData transaction, RuleContext context)
        => transaction.MintedAssets.Keys.Any(pid => _watchedPolicies.Contains(pid));

    public override RuleMatchResult Evaluate(TransactionData transaction, RuleContext context)
    {
        var matchedMints = transaction.MintedAssets
            .Where(kv => _watchedPolicies.Contains(kv.Key))
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        return new(Id, Name, new Dictionary<string, object>
        {
            ["minted_assets"] = matchedMints,
            ["policy_count"] = matchedMints.Count
        });
    }
}
```

### CIP-25 NFT Rule

Matches NFT minting with CIP-25 metadata:

```csharp
public sealed class Cip25NftRule : TransactionRuleBase
{
    private const int Cip25Label = 721;

    public override string Id => "cip25-nft";
    public override string Name => "CIP-25 NFT";
    public override string Description => "Matches NFT minting with CIP-25 metadata";
    public override bool IsEnabled => true;

    public override bool IsMatch(TransactionData transaction, RuleContext context)
        => transaction.MintedAssets.Count > 0 &&
           transaction.Metadata.ContainsKey(Cip25Label);

    public override RuleMatchResult Evaluate(TransactionData transaction, RuleContext context)
    {
        var policies = transaction.MintedAssets.Keys.ToList();
        var metadata = transaction.Metadata.TryGetValue(Cip25Label, out var m) ? m : null;

        return new(Id, Name, new Dictionary<string, object>
        {
            ["policy_ids"] = policies,
            ["asset_count"] = transaction.MintedAssets.Values.Sum(a => a.Count),
            ["has_metadata"] = metadata != null
        });
    }
}
```

## Best Practices

### 1. Keep IsMatch() Fast

The `IsMatch` method is called for every transaction. Keep it lightweight:

```csharp
// Good: Simple, fast check
public override bool IsMatch(TransactionData tx, RuleContext ctx)
    => _addresses.Any(a => tx.AllAddresses.Contains(a));

// Avoid: Complex operations in IsMatch
public override bool IsMatch(TransactionData tx, RuleContext ctx)
{
    // Don't do expensive operations here
    var result = SomeExpensiveComputation(tx);
    return result.IsValid && result.Score > threshold;
}
```

### 2. Use HashSets for Lookups

When matching against lists of values:

```csharp
// Good: O(1) lookup
private readonly HashSet<string> _addresses = new(
    options.Value.Addresses, StringComparer.OrdinalIgnoreCase);

public override bool IsMatch(TransactionData tx, RuleContext ctx)
    => tx.AllAddresses.Any(a => _addresses.Contains(a));

// Avoid: O(n) lookup
public override bool IsMatch(TransactionData tx, RuleContext ctx)
    => tx.AllAddresses.Any(a => _options.Addresses.Contains(a));
```

### 3. Include Meaningful Match Criteria

Return useful information in `MatchedCriteria`:

```csharp
public override RuleMatchResult Evaluate(TransactionData tx, RuleContext ctx)
    => new(Id, Name, new Dictionary<string, object>
    {
        // Include what matched and why
        ["matched_addresses"] = tx.AllAddresses
            .Where(a => _addresses.Contains(a)).ToList(),
        ["match_type"] = "exact_match",
        ["address_count"] = matchedCount
    });
```

### 4. Handle Configuration Changes

Make rules respect enabled/disabled state:

```csharp
public override bool IsEnabled =>
    _options.Enabled &&
    (_options.Addresses.Count > 0 || _options.Prefixes.Count > 0);
```

### 5. Add Comprehensive Tests

Test each rule thoroughly:

```csharp
public class HighFeeRuleTests
{
    [Fact]
    public void IsMatch_WithHighFee_ReturnsTrue()
    {
        var options = Options.Create(new HighFeeRuleOptions
        {
            Enabled = true,
            ThresholdLovelace = 1_000_000
        });
        var rule = new HighFeeRule(options);
        var tx = CreateTransaction(fee: 2_000_000);

        rule.IsMatch(tx, _context).Should().BeTrue();
    }

    [Fact]
    public void IsMatch_WithLowFee_ReturnsFalse()
    {
        var options = Options.Create(new HighFeeRuleOptions
        {
            Enabled = true,
            ThresholdLovelace = 1_000_000
        });
        var rule = new HighFeeRule(options);
        var tx = CreateTransaction(fee: 500_000);

        rule.IsMatch(tx, _context).Should().BeFalse();
    }

    [Fact]
    public void IsMatch_WhenDisabled_RuleNotEnabled()
    {
        var options = Options.Create(new HighFeeRuleOptions
        {
            Enabled = false
        });
        var rule = new HighFeeRule(options);

        rule.IsEnabled.Should().BeFalse();
    }
}
```

## Testing Your Rule

### Unit Tests

```bash
# Run all tests
dotnet test

# Run specific test class
dotnet test --filter "FullyQualifiedName~HighFeeRuleTests"

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Integration Testing

Run against live data:

```bash
# Start infrastructure
docker compose up redis placement -d

# Run with your rule enabled
dapr run --app-id blockchain-events \
         --app-port 5000 \
         --resources-path ./dapr/components \
         -- dotnet run --project src/BlockchainEvents.Worker
```

## Debugging Tips

1. **Log rule matches**: Add logging in `Evaluate()` to see matches
2. **Use AllTransactionsRule**: Temporarily enable to see all transactions
3. **Check Redis Commander**: View events at http://localhost:4006
4. **Inspect CloudEvents**: The event payload includes full transaction data

## Next Steps

- Review [event-schema.md](event-schema.md) for event format details
- See [architecture.md](architecture.md) for system overview
- Check existing rules in `src/BlockchainEvents.Engine/Rules/` for patterns
