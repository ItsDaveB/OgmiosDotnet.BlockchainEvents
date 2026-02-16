namespace BlockchainEvents.Domain.Events;

public static class BlockchainEventFactory
{
    public static BlockchainEvent<TransactionMatchedData> Create(
        RuleMatchResult matchResult,
        TransactionData transaction,
        RuleContext context)
    {
        var timestamp = DateTimeOffset.UtcNow;
        var eventId = $"{transaction.Id}-{matchResult.RuleId}-{timestamp.ToUnixTimeMilliseconds()}";
        var source = $"cardano://{context.Network}/slot/{context.Slot}/block/{context.BlockHash}";
        var eventType = $"io.cardano.transaction.{matchResult.RuleId.ToLowerInvariant()}";

        return new BlockchainEvent<TransactionMatchedData>
        {
            Id = eventId,
            Source = source,
            Type = eventType,
            Subject = matchResult.RuleName,
            Time = timestamp,
            DataSchema = "https://schema.cardano.org/events/transaction-matched/v1",
            Data = new TransactionMatchedData
            {
                TransactionId = transaction.Id,
                Slot = context.Slot,
                BlockHeight = context.BlockHeight,
                BlockHash = context.BlockHash,
                RuleId = matchResult.RuleId,
                RuleName = matchResult.RuleName,
                MatchedCriteria = matchResult.MatchedCriteria,
                Transaction = transaction
            },
            CardanoSlot = context.Slot,
            CardanoBlock = context.BlockHash,
            CardanoBlockHeight = context.BlockHeight,
            CardanoEra = context.Era,
            CardanoNetwork = context.Network
        };
    }
}
