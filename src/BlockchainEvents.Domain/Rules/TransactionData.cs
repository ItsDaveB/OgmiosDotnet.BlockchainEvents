namespace BlockchainEvents.Domain.Rules;

public sealed class TransactionData
{
    public required string Id { get; init; }
    public required long Slot { get; init; }
    public required string BlockHash { get; init; }
    public required long BlockHeight { get; init; }
    public required int Index { get; init; }

    /// <summary>Fee in lovelace.</summary>
    public long Fee { get; init; }

    public IReadOnlyList<string> InputAddresses { get; init; } = [];
    public IReadOnlyList<string> OutputAddresses { get; init; } = [];

    [JsonIgnore]
    public IEnumerable<string> AllAddresses => InputAddresses.Concat(OutputAddresses).Distinct();

    /// <summary>PolicyId → AssetName → Amount (negative = burned).</summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, long>> MintedAssets { get; init; } =
        new Dictionary<string, IReadOnlyDictionary<string, long>>();

    /// <summary>Metadata by label (CIP-10 format).</summary>
    public IReadOnlyDictionary<int, object?> Metadata { get; init; } = new Dictionary<int, object?>();

    public bool HasGovernanceAction { get; init; }
    public bool HasTreasuryWithdrawal { get; init; }
    public bool HasStakeDelegation { get; init; }
    public bool HasStakeRegistration { get; init; }
    public bool HasVote { get; init; }
}
