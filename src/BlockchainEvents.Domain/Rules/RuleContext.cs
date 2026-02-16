namespace BlockchainEvents.Domain.Rules;

public sealed record RuleContext(
    long Slot,
    string BlockHash,
    long BlockHeight,
    string Era,
    string Network,
    DateTimeOffset BlockTime
);
