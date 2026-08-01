using System.Threading.Channels;

namespace BlockchainEvents.Worker.Services;

/// <summary>
/// Thread-safe, in-memory event fan-out using bounded channels.
/// Each gRPC subscriber gets its own channel; slow consumers are dropped to protect the pipeline.
/// </summary>
public sealed class EventBroadcaster(
    ILogger<EventBroadcaster> logger,
    int channelCapacity = 1_000,
    int recentBufferCapacity = 100) : IEventBroadcaster
{
    private readonly object _lock = new();
    private readonly List<ChannelSubscription> _subscriptions = [];
    private readonly Queue<BlockchainEvent<TransactionMatchedData>> _recent = new();

    public int SubscriberCount
    {
        get { lock (_lock) return _subscriptions.Count; }
    }

    public async Task BroadcastAsync(BlockchainEvent<TransactionMatchedData> cloudEvent, CancellationToken ct = default)
    {
        List<ChannelSubscription> snapshot;
        lock (_lock)
        {
            _recent.Enqueue(cloudEvent);
            while (_recent.Count > recentBufferCapacity)
                _recent.Dequeue();
            snapshot = [.. _subscriptions];
        }

        foreach (var sub in snapshot)
        {
            if (!sub.Writer.TryWrite(cloudEvent))
            {
                logger.LogWarning("Subscriber channel full ({Capacity}), dropping event {EventId}",
                    channelCapacity, cloudEvent.Id);
            }
        }

        await Task.CompletedTask;
    }

    public IReadOnlyList<BlockchainEvent<TransactionMatchedData>> GetRecent(int count = 20, string? ruleFilter = null)
    {
        if (count < 1) count = 1;
        if (count > recentBufferCapacity) count = recentBufferCapacity;

        lock (_lock)
        {
            IEnumerable<BlockchainEvent<TransactionMatchedData>> query = _recent;
            if (!string.IsNullOrWhiteSpace(ruleFilter))
                query = query.Where(e => e.Data?.RuleId == ruleFilter);

            return query.TakeLast(count).ToList();
        }
    }

    public IEventSubscription Subscribe()
    {
        var channel = Channel.CreateBounded<BlockchainEvent<TransactionMatchedData>>(
            new BoundedChannelOptions(channelCapacity)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false
            });

        var sub = new ChannelSubscription(channel, this);

        lock (_lock) _subscriptions.Add(sub);
        logger.LogInformation("gRPC subscriber connected (total: {Count})", SubscriberCount);

        return sub;
    }

    private void Unsubscribe(ChannelSubscription sub)
    {
        lock (_lock) _subscriptions.Remove(sub);
        logger.LogInformation("gRPC subscriber disconnected (total: {Count})", SubscriberCount);
    }

    private sealed class ChannelSubscription(
        Channel<BlockchainEvent<TransactionMatchedData>> channel,
        EventBroadcaster broadcaster) : IEventSubscription
    {
        public ChannelWriter<BlockchainEvent<TransactionMatchedData>> Writer => channel.Writer;

        public ValueTask<bool> WaitToReadAsync(CancellationToken ct = default)
            => channel.Reader.WaitToReadAsync(ct);

        public bool TryRead(out BlockchainEvent<TransactionMatchedData>? cloudEvent)
            => channel.Reader.TryRead(out cloudEvent);

        public void Dispose()
        {
            channel.Writer.TryComplete();
            broadcaster.Unsubscribe(this);
        }
    }
}
