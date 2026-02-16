namespace BlockchainEvents.Tests.Checkpoints;

public class SyncCheckpointTests
{
    [Fact]
    public void SyncCheckpoint_CanBeCreated()
    {
        // Arrange & Act
        var checkpoint = new SyncCheckpoint(
            Slot: 12345678,
            BlockHash: "abc123def456",
            BlockHeight: 9876543
        );

        // Assert
        checkpoint.Slot.Should().Be(12345678);
        checkpoint.BlockHash.Should().Be("abc123def456");
        checkpoint.BlockHeight.Should().Be(9876543);
    }

    [Fact]
    public void SyncCheckpoint_CanBeSerialized()
    {
        // Arrange
        var checkpoint = new SyncCheckpoint(
            Slot: 12345678,
            BlockHash: "abc123def456",
            BlockHeight: 9876543
        );

        // Act
        var json = JsonSerializer.Serialize(checkpoint);

        // Assert
        json.Should().Contain("12345678");
        json.Should().Contain("abc123def456");
        json.Should().Contain("9876543");
    }

    [Fact]
    public void SyncCheckpoint_CanBeDeserialized()
    {
        // Arrange
        var json = """{"Slot":12345678,"BlockHash":"abc123def456","BlockHeight":9876543}""";

        // Act
        var checkpoint = JsonSerializer.Deserialize<SyncCheckpoint>(json);

        // Assert
        checkpoint.Should().NotBeNull();
        checkpoint!.Slot.Should().Be(12345678);
        checkpoint.BlockHash.Should().Be("abc123def456");
        checkpoint.BlockHeight.Should().Be(9876543);
    }

    [Fact]
    public void SyncCheckpoint_Equality_WorksCorrectly()
    {
        // Arrange
        var checkpoint1 = new SyncCheckpoint(12345678, "abc123", 9876543);
        var checkpoint2 = new SyncCheckpoint(12345678, "abc123", 9876543);
        var checkpoint3 = new SyncCheckpoint(12345679, "abc123", 9876543);

        // Assert - use BeEquivalentTo which ignores ProcessedAt timestamp differences
        checkpoint1.Should().BeEquivalentTo(checkpoint2, options => options
            .Excluding(c => c.ProcessedAt));
        checkpoint1.Should().NotBe(checkpoint3);
    }

    [Fact]
    public void SyncCheckpoint_RoundTripSerialization_PreservesData()
    {
        // Arrange
        var original = new SyncCheckpoint(
            Slot: 999999999,
            BlockHash: "longhash123456789abcdef",
            BlockHeight: 888888888
        );

        // Act
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<SyncCheckpoint>(json);

        // Assert
        deserialized.Should().Be(original);
    }

    [Fact]
    public void SyncCheckpoint_WithCamelCaseSerialization_Works()
    {
        // Arrange
        var checkpoint = new SyncCheckpoint(12345678, "abc123", 9876543);
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // Act
        var json = JsonSerializer.Serialize(checkpoint, options);
        var deserialized = JsonSerializer.Deserialize<SyncCheckpoint>(json, options);

        // Assert
        json.Should().Contain("\"slot\"");
        json.Should().Contain("\"blockHash\"");
        json.Should().Contain("\"blockHeight\"");
        deserialized.Should().Be(checkpoint);
    }
}
