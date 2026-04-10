namespace BlockchainEvents.Domain.Events;

/// <summary>
/// Broadcasts blockchain events to multiple in-process subscribers (e.g. gRPC streaming clients).
/// </summary>
public interface IEventBroadcaster
{
    /// <summary>Broadcast an event to all connected subscribers.</summary>
    Task BroadcastAsync(BlockchainEvent<TransactionMatchedData> cloudEvent, CancellationToken ct = default);

    /// <summary>Subscribe and receive a channel reader for incoming events.</summary>
    IEventSubscription Subscribe();

    /// <summary>Number of currently connected subscribers.</summary>
    int SubscriberCount { get; }
}

/// <summary>
/// Represents a single subscriber's connection to the event broadcaster.
/// Dispose to unsubscribe.
/// </summary>
public interface IEventSubscription : IDisposable
{
    /// <summary>Reads the next event. Returns false when the subscription is closed.</summary>
    ValueTask<bool> WaitToReadAsync(CancellationToken ct = default);

    /// <summary>Try to read an event without waiting.</summary>
    bool TryRead(out BlockchainEvent<TransactionMatchedData>? cloudEvent);
}
