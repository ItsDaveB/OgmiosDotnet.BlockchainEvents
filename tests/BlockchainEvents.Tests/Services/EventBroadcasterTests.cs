namespace BlockchainEvents.Tests.Services;

public class EventBroadcasterTests
{
    private static EventBroadcaster CreateBroadcaster(int capacity = 100)
        => new(Mock.Of<ILogger<EventBroadcaster>>(), capacity);

    private static BlockchainEvent<TransactionMatchedData> CreateTestEvent(string ruleId = "address-match")
    {
        var tx = new TransactionData
        {
            Id = "tx-" + Guid.NewGuid().ToString("N")[..16],
            Slot = 12345678,
            BlockHash = "blockhash123",
            BlockHeight = 9876543,
            Index = 0,
            Fee = 200000,
            InputAddresses = ["addr_test1_input"],
            OutputAddresses = ["addr_test1_output"]
        };

        return new BlockchainEvent<TransactionMatchedData>
        {
            Id = $"{tx.Id}-{ruleId}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            Source = $"cardano://preprod/slot/{tx.Slot}/block/{tx.BlockHash}",
            Type = $"io.cardano.transaction.{ruleId}",
            Subject = "Test Rule",
            Time = DateTimeOffset.UtcNow,
            DataSchema = "https://schema.cardano.org/events/transaction-matched/v1",
            Data = new TransactionMatchedData
            {
                TransactionId = tx.Id,
                Slot = tx.Slot,
                BlockHeight = tx.BlockHeight,
                BlockHash = tx.BlockHash,
                RuleId = ruleId,
                RuleName = "Test Rule",
                MatchedCriteria = new Dictionary<string, object> { ["test"] = "value" },
                Transaction = tx
            },
            CardanoSlot = tx.Slot,
            CardanoBlock = tx.BlockHash,
            CardanoBlockHeight = tx.BlockHeight,
            CardanoEra = "Conway",
            CardanoNetwork = "preprod"
        };
    }

    [Fact]
    public void SubscriberCount_InitiallyZero()
    {
        var broadcaster = CreateBroadcaster();
        broadcaster.SubscriberCount.Should().Be(0);
    }

    [Fact]
    public void Subscribe_IncreasesSubscriberCount()
    {
        var broadcaster = CreateBroadcaster();

        using var sub1 = broadcaster.Subscribe();
        broadcaster.SubscriberCount.Should().Be(1);

        using var sub2 = broadcaster.Subscribe();
        broadcaster.SubscriberCount.Should().Be(2);
    }

    [Fact]
    public void Dispose_DecreasesSubscriberCount()
    {
        var broadcaster = CreateBroadcaster();

        var sub1 = broadcaster.Subscribe();
        var sub2 = broadcaster.Subscribe();
        broadcaster.SubscriberCount.Should().Be(2);

        sub1.Dispose();
        broadcaster.SubscriberCount.Should().Be(1);

        sub2.Dispose();
        broadcaster.SubscriberCount.Should().Be(0);
    }

    [Fact]
    public async Task BroadcastAsync_DeliversEventToSubscriber()
    {
        var broadcaster = CreateBroadcaster();
        using var subscription = broadcaster.Subscribe();

        var testEvent = CreateTestEvent();
        await broadcaster.BroadcastAsync(testEvent);

        subscription.TryRead(out var received).Should().BeTrue();
        received.Should().NotBeNull();
        received!.Id.Should().Be(testEvent.Id);
        received.Data!.TransactionId.Should().Be(testEvent.Data!.TransactionId);
    }

    [Fact]
    public async Task BroadcastAsync_DeliversEventToMultipleSubscribers()
    {
        var broadcaster = CreateBroadcaster();
        using var sub1 = broadcaster.Subscribe();
        using var sub2 = broadcaster.Subscribe();

        var testEvent = CreateTestEvent();
        await broadcaster.BroadcastAsync(testEvent);

        sub1.TryRead(out var received1).Should().BeTrue();
        sub2.TryRead(out var received2).Should().BeTrue();
        received1!.Id.Should().Be(testEvent.Id);
        received2!.Id.Should().Be(testEvent.Id);
    }

    [Fact]
    public async Task BroadcastAsync_NoSubscribers_DoesNotThrow()
    {
        var broadcaster = CreateBroadcaster();
        var testEvent = CreateTestEvent();

        var act = async () => await broadcaster.BroadcastAsync(testEvent);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void TryRead_ReturnsFalse_WhenNoEvents()
    {
        var broadcaster = CreateBroadcaster();
        using var subscription = broadcaster.Subscribe();

        subscription.TryRead(out var received).Should().BeFalse();
        received.Should().BeNull();
    }

    [Fact]
    public async Task WaitToReadAsync_CompletesWhenEventBroadcast()
    {
        var broadcaster = CreateBroadcaster();
        using var subscription = broadcaster.Subscribe();

        var testEvent = CreateTestEvent();

        // Broadcast after a small delay
        _ = Task.Run(async () =>
        {
            await Task.Delay(50);
            await broadcaster.BroadcastAsync(testEvent);
        });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var canRead = await subscription.WaitToReadAsync(cts.Token);
        canRead.Should().BeTrue();

        subscription.TryRead(out var received).Should().BeTrue();
        received!.Id.Should().Be(testEvent.Id);
    }

    [Fact]
    public async Task BroadcastAsync_DropsOldestWhenChannelFull()
    {
        // Create broadcaster with tiny capacity
        var broadcaster = CreateBroadcaster(capacity: 3);
        using var subscription = broadcaster.Subscribe();

        // Broadcast more events than capacity
        var events = Enumerable.Range(0, 5)
            .Select(i => CreateTestEvent($"rule-{i}"))
            .ToList();

        foreach (var e in events)
            await broadcaster.BroadcastAsync(e);

        // Should be able to read events (oldest may have been dropped)
        var readEvents = new List<BlockchainEvent<TransactionMatchedData>>();
        while (subscription.TryRead(out var received))
        {
            if (received is not null)
                readEvents.Add(received);
        }

        // Channel capacity is 3, so at most 3 events should be readable
        readEvents.Count.Should().BeInRange(1, 3);
        // The newest events should be preserved (DropOldest policy)
        readEvents.Last().Data!.RuleId.Should().Be("rule-4");
    }

    [Fact]
    public async Task Subscribe_AfterBroadcast_DoesNotReceiveOldEvents()
    {
        var broadcaster = CreateBroadcaster();

        var testEvent = CreateTestEvent();
        await broadcaster.BroadcastAsync(testEvent);

        // Subscribe after the event was broadcast
        using var subscription = broadcaster.Subscribe();
        subscription.TryRead(out var received).Should().BeFalse();
    }

    [Fact]
    public async Task Dispose_StopsReceivingEvents()
    {
        var broadcaster = CreateBroadcaster();
        var subscription = broadcaster.Subscribe();
        subscription.Dispose();

        // Broadcast after dispose
        var testEvent = CreateTestEvent();
        await broadcaster.BroadcastAsync(testEvent);

        // TryRead should return false since the channel is completed
        subscription.TryRead(out _).Should().BeFalse();
    }

    [Fact]
    public async Task ConcurrentBroadcast_AllEventsDelivered()
    {
        var broadcaster = CreateBroadcaster(capacity: 1000);
        using var subscription = broadcaster.Subscribe();

        var tasks = Enumerable.Range(0, 50)
            .Select(i => broadcaster.BroadcastAsync(CreateTestEvent($"rule-{i}")))
            .ToArray();

        await Task.WhenAll(tasks);

        var count = 0;
        while (subscription.TryRead(out var received))
        {
            if (received is not null) count++;
        }

        count.Should().Be(50);
    }

    [Fact]
    public async Task GetRecent_ReturnsNewestEvents_WithOptionalRuleFilter()
    {
        var broadcaster = CreateBroadcaster();
        await broadcaster.BroadcastAsync(CreateTestEvent("address-match"));
        await broadcaster.BroadcastAsync(CreateTestEvent("metadata-key-value"));
        await broadcaster.BroadcastAsync(CreateTestEvent("address-match"));

        var all = broadcaster.GetRecent(10);
        all.Should().HaveCount(3);

        var addressOnly = broadcaster.GetRecent(10, "address-match");
        addressOnly.Should().HaveCount(2);
        addressOnly.Should().OnlyContain(e => e.Data.RuleId == "address-match");
    }
}
