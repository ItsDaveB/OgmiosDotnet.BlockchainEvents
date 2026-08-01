namespace BlockchainEvents.Worker.Endpoints;

public static class SubscriptionEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static long _eventsReceived;
    private static long _lastEventTimeTicks = DateTimeOffset.MinValue.Ticks;

    public static void MapSubscriptionEndpoints(this WebApplication app)
    {
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Subscriptions");

        app.MapGet("/dapr/subscribe", () => Results.Json(new[]
        {
            new { pubsubname = "pubsub", topic = "blockchain-events", route = "/events/blockchain" }
        }))
        .WithName("DaprSubscribe")
        .WithTags("Dapr")
        .WithDescription("Dapr pub/sub subscription discovery document.");

        app.MapPost("/events/blockchain", async (HttpContext context) =>
        {
            try
            {
                var daprEnvelope = await JsonSerializer.DeserializeAsync<JsonElement>(
                    context.Request.Body, JsonOptions);

                BlockchainEvent<TransactionMatchedData>? blockchainEvent = null;

                if (daprEnvelope.TryGetProperty("data", out var dataElement))
                {
                    blockchainEvent = dataElement.Deserialize<BlockchainEvent<TransactionMatchedData>>(JsonOptions);
                }
                else
                {
                    blockchainEvent = daprEnvelope.Deserialize<BlockchainEvent<TransactionMatchedData>>(JsonOptions);
                }

                Interlocked.Increment(ref _eventsReceived);
                Interlocked.Exchange(ref _lastEventTimeTicks, DateTimeOffset.UtcNow.Ticks);

                var txId = blockchainEvent?.Data?.TransactionId ?? "unknown";
                var slot = blockchainEvent?.CardanoSlot;

                logger.LogInformation("📥 Event #{Count}: Transaction {Tx}, Slot {Slot}",
                    Interlocked.Read(ref _eventsReceived), txId, slot);

                return Results.Ok();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing event");
                return Results.StatusCode(500);
            }
        })
        .WithName("ReceiveBlockchainEvent")
        .WithTags("Dapr")
        .WithDescription("Dapr pub/sub delivery endpoint for blockchain CloudEvents.");

        app.MapGet("/subscriptions/status", (IEventBroadcaster broadcaster) =>
        {
            var ticks = Interlocked.Read(ref _lastEventTimeTicks);
            var lastTime = ticks == DateTimeOffset.MinValue.Ticks
                ? null
                : new DateTimeOffset(ticks, TimeSpan.Zero).ToString("o");
            var recent = broadcaster.GetRecent(100);
            var demoMode = Environment.GetEnvironmentVariable("DEMO_EVENTS") == "true";

            return new
            {
                eventsReceived = Interlocked.Read(ref _eventsReceived),
                lastEventTime = lastTime,
                daprDeliveryPath = "/events/blockchain",
                activeStreamSubscribers = broadcaster.SubscriberCount,
                recentBroadcastCount = recent.Count,
                demoEvents = demoMode,
                note = "eventsReceived counts CloudEvents delivered by Dapr to POST /events/blockchain. " +
                       "recentBroadcastCount is the in-process fan-out buffer used by SSE/gRPC."
            };
        })
        .WithName("SubscriptionStatus")
        .WithTags("Subscriptions")
        .WithDescription(
            "Subscription activity: Dapr HTTP deliveries (eventsReceived) plus in-process broadcast buffer size. " +
            "With DEMO_EVENTS=true, events are published to Dapr so this counter should increase.");
    }
}
