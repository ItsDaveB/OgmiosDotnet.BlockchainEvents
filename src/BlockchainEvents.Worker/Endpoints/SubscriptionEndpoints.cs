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
        }));

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
        });

        app.MapGet("/subscriptions/status", () =>
        {
            var ticks = Interlocked.Read(ref _lastEventTimeTicks);
            var lastTime = ticks == DateTimeOffset.MinValue.Ticks ? null : new DateTimeOffset(ticks, TimeSpan.Zero).ToString("o");
            return new
            {
                eventsReceived = Interlocked.Read(ref _eventsReceived),
                lastEventTime = lastTime
            };
        });
    }
}
