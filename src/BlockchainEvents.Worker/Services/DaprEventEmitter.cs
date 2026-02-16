namespace BlockchainEvents.Worker.Services;

/// <summary>
/// Emits blockchain events via Dapr pub/sub. Failed events are sent to a dead letter queue.
/// </summary>
public sealed class DaprEventEmitter(
    DaprClient daprClient,
    IOptions<BlockchainEventsOptions> options,
    IEventMetrics metrics,
    ILogger<DaprEventEmitter> logger) : IBlockchainEventEmitter
{
    private readonly BlockchainEventsOptions _options = options.Value;

    public async Task EmitAsync(
        TransactionData transaction,
        RuleMatchResult matchResult,
        RuleContext context,
        CancellationToken ct = default)
    {
        var timer = metrics.StartTimer();
        var cloudEvent = BlockchainEventFactory.Create(matchResult, transaction, context);

        try
        {
            if (_options.UseRawPayload)
            {
                await daprClient.PublishEventAsync(
                    _options.PubSubName, _options.TopicName, cloudEvent.Data,
                    new Dictionary<string, string> { ["rawPayload"] = "true" }, ct);
            }
            else
            {
                await daprClient.PublishEventAsync(_options.PubSubName, _options.TopicName, cloudEvent, ct);
            }

            metrics.RecordEventEmitted(matchResult.RuleName);
            logger.LogDebug("Emitted event for {TransactionId} matched by {Rule}", transaction.Id, matchResult.RuleName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to emit event for {TransactionId}, sending to DLQ", transaction.Id);
            metrics.RecordEventFailed(matchResult.RuleName, ex.GetType().Name);

            await SendToDeadLetterQueueAsync(cloudEvent, transaction, matchResult, ex, ct);
        }
        finally
        {
            timer.Stop();
            metrics.RecordProcessingLatency(timer.ElapsedMilliseconds, matchResult.RuleName);
            EventMetrics.CompleteProcessing();
        }
    }

    private async Task SendToDeadLetterQueueAsync(
        BlockchainEvent<TransactionMatchedData> cloudEvent,
        TransactionData transaction,
        RuleMatchResult matchResult,
        Exception exception,
        CancellationToken ct)
    {
        try
        {
            var dlqPayload = new DeadLetterEvent
            {
                OriginalEvent = cloudEvent,
                TransactionId = transaction.Id,
                RuleName = matchResult.RuleName,
                ErrorMessage = exception.Message,
                ErrorType = exception.GetType().Name,
                FailedAt = DateTimeOffset.UtcNow,
                RetryCount = 0
            };

            await daprClient.PublishEventAsync(_options.PubSubName, _options.DeadLetterTopicName, dlqPayload, ct);
            logger.LogWarning("Sent failed event {TransactionId} to DLQ topic {DlqTopic}",
                transaction.Id, _options.DeadLetterTopicName);
        }
        catch (Exception dlqEx)
        {
            logger.LogCritical(dlqEx, "Failed to send event {TransactionId} to DLQ - event lost!", transaction.Id);
        }
    }
}

public sealed class DeadLetterEvent
{
    public BlockchainEvent<TransactionMatchedData>? OriginalEvent { get; set; }
    public string TransactionId { get; set; } = string.Empty;
    public string RuleName { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public string ErrorType { get; set; } = string.Empty;
    public DateTimeOffset FailedAt { get; set; }
    public int RetryCount { get; set; }
}
