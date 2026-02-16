namespace BlockchainEvents.Domain.Checkpoints;

public sealed record SyncCheckpoint(
    long Slot,
    string BlockHash,
    long BlockHeight
)
{
    public DateTimeOffset ProcessedAt { get; init; } = DateTimeOffset.UtcNow;
    public long TransactionsProcessed { get; init; } = 0;
    public long EventsEmitted { get; init; } = 0;

    public static SyncCheckpoint Origin => new(
        Slot: 0,
        BlockHash: "origin",
        BlockHeight: 0
    );

    public static SyncCheckpoint FromSlot(long slot, string blockHash, long blockHeight) => new(
        Slot: slot,
        BlockHash: blockHash,
        BlockHeight: blockHeight
    );

    public SyncCheckpoint WithProgress(int transactionsProcessed, int eventsEmitted) => this with
    {
        ProcessedAt = DateTimeOffset.UtcNow,
        TransactionsProcessed = TransactionsProcessed + transactionsProcessed,
        EventsEmitted = EventsEmitted + eventsEmitted
    };
}
