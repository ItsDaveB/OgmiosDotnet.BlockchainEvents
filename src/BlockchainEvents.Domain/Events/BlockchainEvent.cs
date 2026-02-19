namespace BlockchainEvents.Domain.Events;

public sealed class BlockchainEvent<TData> where TData : class
{
    public string SpecVersion => "1.0";

    /// <summary>Format: {transactionId}-{ruleId}-{timestamp}</summary>
    public required string Id { get; init; }

    /// <summary>Format: cardano://{network}/slot/{slot}/block/{blockHash}</summary>
    public required string Source { get; init; }

    /// <summary>Format: io.cardano.transaction.{ruleId}</summary>
    public required string Type { get; init; }

    public string? Subject { get; init; }
    public required DateTimeOffset Time { get; init; }
    public string DataContentType => "application/json";
    public string? DataSchema { get; init; }
    public required TData Data { get; init; }

    public long CardanoSlot { get; init; }
    public string? CardanoBlock { get; init; }
    public long CardanoBlockHeight { get; init; }
    public string? CardanoEra { get; init; }
    public string? CardanoNetwork { get; init; }
}

public sealed class TransactionMatchedData
{
    public required string TransactionId { get; init; }
    public required long Slot { get; init; }
    public required long BlockHeight { get; init; }
    public required string BlockHash { get; init; }
    public required string RuleId { get; init; }
    public required string RuleName { get; init; }
    public required IReadOnlyDictionary<string, object> MatchedCriteria { get; init; }
    public required TransactionData Transaction { get; init; }
}
