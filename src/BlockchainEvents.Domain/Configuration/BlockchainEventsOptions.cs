namespace BlockchainEvents.Domain.Configuration;

public sealed class BlockchainEventsOptions
{
    public const string SectionName = "BlockchainEvents";

    public string Network { get; set; } = "preprod";
    public string PubSubName { get; set; } = "pubsub";
    public string TopicName { get; set; } = "blockchain-events";
    public string DeadLetterTopicName { get; set; } = "blockchain-events-dlq";
    public string StateStoreName { get; set; } = "statestore";
    public string CheckpointKey { get; set; } = "sync-checkpoint";
    public bool UseRawPayload { get; set; } = false;
    public bool EnableMetrics { get; set; } = true;
}
