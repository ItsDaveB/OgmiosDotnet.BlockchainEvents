namespace BlockchainEvents.Tests.Services;

public class DaprCheckpointServiceTests
{
    private const string StoreName = "statestore";
    private const string CheckpointKey = "sync-checkpoint";

    private static DaprCheckpointService CreateService(DaprClient daprClient)
    {
        var options = Options.Create(new BlockchainEventsOptions
        {
            StateStoreName = StoreName,
            CheckpointKey = CheckpointKey
        });
        var logger = Mock.Of<ILogger<DaprCheckpointService>>();
        return new DaprCheckpointService(daprClient, options, logger);
    }

    [Fact]
    public async Task GetCheckpointAsync_ReturnsCheckpoint_WhenExists()
    {
        // Arrange
        var mockDaprClient = new Mock<DaprClient>();
        var expectedCheckpoint = new SyncCheckpoint(12345678, "abc123", 9876543);

        mockDaprClient
            .Setup(c => c.GetStateAndETagAsync<SyncCheckpoint>(
                StoreName,
                CheckpointKey,
                It.IsAny<ConsistencyMode?>(),
                It.IsAny<IReadOnlyDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((expectedCheckpoint, "etag123"));

        var service = CreateService(mockDaprClient.Object);

        // Act
        var result = await service.GetCheckpointAsync();

        // Assert
        result.Should().NotBeNull();
        result!.Slot.Should().Be(12345678);
        result.BlockHash.Should().Be("abc123");
        result.BlockHeight.Should().Be(9876543);
    }

    [Fact]
    public async Task GetCheckpointAsync_ReturnsNull_WhenNotExists()
    {
        // Arrange
        var mockDaprClient = new Mock<DaprClient>();

#pragma warning disable CS8620 // Dapr returns nullable checkpoint in practice when state doesn't exist
        mockDaprClient
            .Setup(c => c.GetStateAndETagAsync<SyncCheckpoint>(
                StoreName,
                CheckpointKey,
                It.IsAny<ConsistencyMode?>(),
                It.IsAny<IReadOnlyDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult<(SyncCheckpoint, string?)>((null!, null)));
#pragma warning restore CS8620

        var service = CreateService(mockDaprClient.Object);

        // Act
        var result = await service.GetCheckpointAsync();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SaveCheckpointAsync_SavesCheckpointWithETag()
    {
        // Arrange
        var mockDaprClient = new Mock<DaprClient>();
        var existingCheckpoint = new SyncCheckpoint(12345670, "initial123", 9876540);
        var newCheckpoint = new SyncCheckpoint(12345678, "abc123", 9876543);

        // First call - get existing checkpoint to populate ETag
        mockDaprClient
            .Setup(c => c.GetStateAndETagAsync<SyncCheckpoint>(
                StoreName,
                CheckpointKey,
                It.IsAny<ConsistencyMode?>(),
                It.IsAny<IReadOnlyDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((existingCheckpoint, "etag123"));

        mockDaprClient
            .Setup(c => c.TrySaveStateAsync(
                StoreName,
                CheckpointKey,
                newCheckpoint,
                "etag123",
                It.IsAny<StateOptions?>(),
                It.IsAny<IReadOnlyDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = CreateService(mockDaprClient.Object);

        // First get to populate ETag
        await service.GetCheckpointAsync();

        // Act
        await service.SaveCheckpointAsync(newCheckpoint);

        // Assert
        mockDaprClient.Verify(c => c.TrySaveStateAsync(
            StoreName,
            CheckpointKey,
            newCheckpoint,
            "etag123",
            It.IsAny<StateOptions?>(),
            It.IsAny<IReadOnlyDictionary<string, string>?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SaveCheckpointAsync_ThrowsOnConcurrencyConflict()
    {
        // Arrange
        var mockDaprClient = new Mock<DaprClient>();
        var existingCheckpoint = new SyncCheckpoint(12345670, "initial123", 9876540);
        var checkpoint = new SyncCheckpoint(12345678, "abc123", 9876543);

        // First call - get existing checkpoint to populate ETag
        mockDaprClient
            .Setup(c => c.GetStateAndETagAsync<SyncCheckpoint>(
                StoreName,
                CheckpointKey,
                It.IsAny<ConsistencyMode?>(),
                It.IsAny<IReadOnlyDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((existingCheckpoint, "etag123"));

        // Simulate first-write-wins failure
        mockDaprClient
            .Setup(c => c.TrySaveStateAsync(
                StoreName,
                CheckpointKey,
                checkpoint,
                "etag123",
                It.IsAny<StateOptions?>(),
                It.IsAny<IReadOnlyDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var service = CreateService(mockDaprClient.Object);

        // First get to populate ETag
        await service.GetCheckpointAsync();

        // Act
        var act = async () => await service.SaveCheckpointAsync(checkpoint);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*concurrent*");
    }

    [Fact]
    public async Task DeleteCheckpointAsync_DeletesCheckpoint()
    {
        // Arrange
        var mockDaprClient = new Mock<DaprClient>();

        mockDaprClient
            .Setup(c => c.DeleteStateAsync(
                StoreName,
                CheckpointKey,
                It.IsAny<StateOptions?>(),
                It.IsAny<IReadOnlyDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService(mockDaprClient.Object);

        // Act
        await service.DeleteCheckpointAsync();

        // Assert
        mockDaprClient.Verify(c => c.DeleteStateAsync(
            StoreName,
            CheckpointKey,
            It.IsAny<StateOptions?>(),
            It.IsAny<IReadOnlyDictionary<string, string>?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
