namespace BlockchainEvents.Worker.Services;

public sealed class TransactionDataBuilder
{
    private readonly Transaction _tx;
    private readonly long _slot;
    private readonly int _index;
    private readonly string _blockHash;
    private readonly long _blockHeight;

    private string? _id;
    private long _fee;
    private IReadOnlyList<string> _inputAddresses = [];
    private IReadOnlyList<string> _outputAddresses = [];
    private IReadOnlyDictionary<string, IReadOnlyDictionary<string, long>> _mintedAssets =
        new Dictionary<string, IReadOnlyDictionary<string, long>>();
    private IReadOnlyDictionary<int, object?> _metadata = new Dictionary<int, object?>();
    private CertificateFlags _certificateFlags;
    private GovernanceFlags _governanceFlags;

    private TransactionDataBuilder(Transaction tx, string blockHash, long blockHeight, long slot, int index)
    {
        _tx = tx;
        _blockHash = blockHash;
        _blockHeight = blockHeight;
        _slot = slot;
        _index = index;
    }

    public static TransactionDataBuilder For(Transaction tx, string blockHash, long blockHeight, long slot, int index)
        => new(tx, blockHash, blockHeight, slot, index);

    public TransactionDataBuilder WithIdentity()
    {
        var extractor = TransactionIdExtractor.Instance;
        _id = extractor.Extract(_tx, _slot, _index);
        _fee = extractor.ExtractFee(_tx);
        return this;
    }

    public TransactionDataBuilder WithAddresses()
    {
        _outputAddresses = AddressExtractor.Instance.Extract(_tx);
        return this;
    }

    public TransactionDataBuilder WithAssets()
    {
        _mintedAssets = AssetExtractor.Instance.Extract(_tx);
        return this;
    }

    public TransactionDataBuilder WithMetadata()
    {
        _metadata = MetadataExtractor.Instance.Extract(_tx);
        return this;
    }

    public TransactionDataBuilder WithCertificates()
    {
        _certificateFlags = CertificateExtractor.Instance.Extract(_tx);
        return this;
    }

    public TransactionDataBuilder WithGovernance()
    {
        _governanceFlags = GovernanceExtractor.Instance.Extract(_tx);
        return this;
    }

    public TransactionDataBuilder WithAllPraosData() =>
        WithIdentity()
        .WithAddresses()
        .WithAssets()
        .WithMetadata()
        .WithCertificates()
        .WithGovernance();

    public TransactionDataBuilder WithByronData() =>
        WithIdentity()
        .WithAddresses();

    public TransactionData Build() => new()
    {
        Id = _id ?? $"tx-{_slot}-{_index}",
        Slot = _slot,
        BlockHash = _blockHash,
        BlockHeight = _blockHeight,
        Index = _index,
        Fee = _fee,
        InputAddresses = _inputAddresses.ToList(),
        OutputAddresses = _outputAddresses.ToList(),
        MintedAssets = _mintedAssets,
        Metadata = _metadata.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value),
        HasGovernanceAction = _certificateFlags.HasGovernance || _governanceFlags.HasProposals,
        HasTreasuryWithdrawal = false,
        HasStakeDelegation = _certificateFlags.HasStakeDelegation,
        HasStakeRegistration = _certificateFlags.HasStakeRegistration,
        HasVote = _governanceFlags.HasVotes
    };
}
