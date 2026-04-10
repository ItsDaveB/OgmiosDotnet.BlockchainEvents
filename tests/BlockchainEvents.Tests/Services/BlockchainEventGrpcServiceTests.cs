using Grpc.Core;
using BlockchainEvents.Worker.Grpc;

namespace BlockchainEvents.Tests.Services;

public class BlockchainEventGrpcServiceTests
{
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
                MatchedCriteria = new Dictionary<string, object> { ["test_key"] = "test_value" },
                Transaction = tx
            },
            CardanoSlot = tx.Slot,
            CardanoBlock = tx.BlockHash,
            CardanoBlockHeight = tx.BlockHeight,
            CardanoEra = "Conway",
            CardanoNetwork = "preprod"
        };
    }

    private static BlockchainEventGrpcService CreateService(IEventBroadcaster broadcaster)
        => new(broadcaster, Mock.Of<ILogger<BlockchainEventGrpcService>>());

    [Fact]
    public async Task GetStatus_ReturnsSubscriberCount()
    {
        // Arrange
        var mockBroadcaster = new Mock<IEventBroadcaster>();
        mockBroadcaster.Setup(b => b.SubscriberCount).Returns(3);

        var service = CreateService(mockBroadcaster.Object);
        var context = new Mock<ServerCallContext> { DefaultValue = DefaultValue.Mock };

        // Act
        var response = await service.GetStatus(new StatusRequest(), context.Object);

        // Assert
        response.ActiveGrpcSubscribers.Should().Be(3);
        response.Uptime.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Subscribe_StreamsEventsToClient()
    {
        // Arrange — use a real EventBroadcaster to verify integration
        var broadcaster = new EventBroadcaster(Mock.Of<ILogger<EventBroadcaster>>());
        var service = CreateService(broadcaster);

        var writtenMessages = new List<BlockchainEventMessage>();
        var mockStream = new Mock<IServerStreamWriter<BlockchainEventMessage>>();
        mockStream
            .Setup(s => s.WriteAsync(It.IsAny<BlockchainEventMessage>(), It.IsAny<CancellationToken>()))
            .Callback<BlockchainEventMessage, CancellationToken>((msg, _) => writtenMessages.Add(msg))
            .Returns(Task.CompletedTask);

        using var cts = new CancellationTokenSource();
        var mockContext = CreateMockServerCallContext(cts.Token);

        var testEvent = CreateTestEvent();

        // Act — start subscription in background
        var subscribeTask = service.Subscribe(new SubscribeRequest(), mockStream.Object, mockContext);

        // Wait for subscription to be registered
        await WaitForConditionAsync(() => broadcaster.SubscriberCount >= 1);

        // Broadcast an event
        await broadcaster.BroadcastAsync(testEvent);

        // Give time for the event to propagate
        await WaitForConditionAsync(() => writtenMessages.Count >= 1);

        // Cancel to stop the subscription
        cts.Cancel();
        await subscribeTask; // Should complete cleanly after cancellation

        // Assert
        writtenMessages.Should().HaveCount(1);
        writtenMessages[0].Id.Should().Be(testEvent.Id);
        writtenMessages[0].SpecVersion.Should().Be("1.0");
        writtenMessages[0].Source.Should().Be(testEvent.Source);
        writtenMessages[0].Type.Should().Be(testEvent.Type);
        writtenMessages[0].CardanoSlot.Should().Be(testEvent.CardanoSlot);
        writtenMessages[0].CardanoNetwork.Should().Be("preprod");
    }

    [Fact]
    public async Task Subscribe_WithRuleFilter_FiltersEvents()
    {
        // Arrange
        var broadcaster = new EventBroadcaster(Mock.Of<ILogger<EventBroadcaster>>());
        var service = CreateService(broadcaster);

        var writtenMessages = new List<BlockchainEventMessage>();
        var mockStream = new Mock<IServerStreamWriter<BlockchainEventMessage>>();
        mockStream
            .Setup(s => s.WriteAsync(It.IsAny<BlockchainEventMessage>(), It.IsAny<CancellationToken>()))
            .Callback<BlockchainEventMessage, CancellationToken>((msg, _) => writtenMessages.Add(msg))
            .Returns(Task.CompletedTask);

        using var cts = new CancellationTokenSource();
        var mockContext = CreateMockServerCallContext(cts.Token);

        // Act — subscribe with filter for "governance-treasury" only
        var request = new SubscribeRequest { RuleFilter = "governance-treasury" };
        var subscribeTask = service.Subscribe(request, mockStream.Object, mockContext);

        await WaitForConditionAsync(() => broadcaster.SubscriberCount >= 1);

        // Broadcast two events: one matching, one not
        await broadcaster.BroadcastAsync(CreateTestEvent("address-match"));
        await broadcaster.BroadcastAsync(CreateTestEvent("governance-treasury"));

        await WaitForConditionAsync(() => writtenMessages.Count >= 1);

        cts.Cancel();
        await subscribeTask;

        // Assert — only governance event should pass
        writtenMessages.Should().HaveCount(1);
        writtenMessages[0].Data!.RuleId.Should().Be("governance-treasury");
    }

    [Fact]
    public async Task Subscribe_MapsAllCloudEventsFields()
    {
        // Arrange
        var broadcaster = new EventBroadcaster(Mock.Of<ILogger<EventBroadcaster>>());
        var service = CreateService(broadcaster);

        var writtenMessages = new List<BlockchainEventMessage>();
        var mockStream = new Mock<IServerStreamWriter<BlockchainEventMessage>>();
        mockStream
            .Setup(s => s.WriteAsync(It.IsAny<BlockchainEventMessage>(), It.IsAny<CancellationToken>()))
            .Callback<BlockchainEventMessage, CancellationToken>((msg, _) => writtenMessages.Add(msg))
            .Returns(Task.CompletedTask);

        using var cts = new CancellationTokenSource();
        var mockContext = CreateMockServerCallContext(cts.Token);

        var testEvent = CreateTestEvent();

        var subscribeTask = service.Subscribe(new SubscribeRequest(), mockStream.Object, mockContext);
        await WaitForConditionAsync(() => broadcaster.SubscriberCount >= 1);

        await broadcaster.BroadcastAsync(testEvent);
        await WaitForConditionAsync(() => writtenMessages.Count >= 1);

        cts.Cancel();
        await subscribeTask;

        // Assert — full CloudEvents mapping
        var msg = writtenMessages[0];
        msg.SpecVersion.Should().Be("1.0");
        msg.Id.Should().Be(testEvent.Id);
        msg.Source.Should().Be(testEvent.Source);
        msg.Type.Should().Be(testEvent.Type);
        msg.Subject.Should().Be("Test Rule");
        msg.Time.Should().NotBeNullOrEmpty();
        msg.DataContentType.Should().Be("application/json");
        msg.DataSchema.Should().Be("https://schema.cardano.org/events/transaction-matched/v1");

        // Cardano extensions
        msg.CardanoSlot.Should().Be(12345678);
        msg.CardanoBlock.Should().Be("blockhash123");
        msg.CardanoBlockHeight.Should().Be(9876543);
        msg.CardanoEra.Should().Be("Conway");
        msg.CardanoNetwork.Should().Be("preprod");

        // Payload
        var data = msg.Data!;
        data.TransactionId.Should().Be(testEvent.Data!.TransactionId);
        data.RuleId.Should().Be("address-match");
        data.RuleName.Should().Be("Test Rule");
        data.Slot.Should().Be(12345678);
        data.BlockHeight.Should().Be(9876543);
        data.BlockHash.Should().Be("blockhash123");
        data.MatchedCriteria.Should().ContainKey("test_key");
        data.MatchedCriteria["test_key"].Should().Be("test_value");

        // Transaction
        var tx = data.Transaction!;
        tx.Id.Should().Be(testEvent.Data.TransactionId);
        tx.Fee.Should().Be(200000);
        tx.InputAddresses.Should().Contain("addr_test1_input");
        tx.OutputAddresses.Should().Contain("addr_test1_output");
    }

    [Fact]
    public async Task Subscribe_CleansUpOnCancellation()
    {
        // Arrange
        var broadcaster = new EventBroadcaster(Mock.Of<ILogger<EventBroadcaster>>());
        var service = CreateService(broadcaster);

        var mockStream = new Mock<IServerStreamWriter<BlockchainEventMessage>>();
        mockStream
            .Setup(s => s.WriteAsync(It.IsAny<BlockchainEventMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        using var cts = new CancellationTokenSource();
        var mockContext = CreateMockServerCallContext(cts.Token);

        // Act
        var subscribeTask = service.Subscribe(new SubscribeRequest(), mockStream.Object, mockContext);
        await WaitForConditionAsync(() => broadcaster.SubscriberCount >= 1);

        broadcaster.SubscriberCount.Should().Be(1);

        cts.Cancel();
        await subscribeTask;

        // Assert — subscriber should be cleaned up
        broadcaster.SubscriberCount.Should().Be(0);
    }

    private static ServerCallContext CreateMockServerCallContext(CancellationToken ct)
        => new TestServerCallContext(ct);

    /// <summary>
    /// Concrete ServerCallContext for testing — Moq cannot override sealed CancellationToken property.
    /// </summary>
    private sealed class TestServerCallContext(CancellationToken ct) : ServerCallContext
    {
        protected override CancellationToken CancellationTokenCore => ct;
        protected override string MethodCore => "TestMethod";
        protected override string HostCore => "localhost";
        protected override string PeerCore => "test-peer";
        protected override DateTime DeadlineCore => DateTime.MaxValue;
        protected override Metadata RequestHeadersCore => [];
        protected override Metadata ResponseTrailersCore => [];
        protected override Status StatusCore { get; set; }
        protected override WriteOptions? WriteOptionsCore { get; set; }
        protected override AuthContext AuthContextCore => new(string.Empty, new Dictionary<string, List<AuthProperty>>());

        protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options) =>
            throw new NotImplementedException();

        protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders) => Task.CompletedTask;
    }

    private static async Task WaitForConditionAsync(Func<bool> condition, int timeoutMs = 5000)
    {
        using var cts = new CancellationTokenSource(timeoutMs);
        while (!condition() && !cts.IsCancellationRequested)
        {
            await Task.Delay(10, cts.Token).ConfigureAwait(false);
        }
    }
}
