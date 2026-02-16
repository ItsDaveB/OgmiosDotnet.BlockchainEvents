namespace BlockchainEvents.Worker.Endpoints;

public static class SubscriptionEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static long _eventsReceived;
    private static DateTimeOffset _lastEventTime = DateTimeOffset.MinValue;

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
                var blockchainEvent = await JsonSerializer.DeserializeAsync<BlockchainEvent<TransactionMatchedData>>(
                    context.Request.Body, JsonOptions);

                _eventsReceived++;
                _lastEventTime = DateTimeOffset.UtcNow;

                var txId = blockchainEvent?.Data?.TransactionId ?? "unknown";
                var slot = blockchainEvent?.CardanoSlot;

                logger.LogInformation("📥 Event #{Count}: Transaction {Tx}, Slot {Slot}",
                    _eventsReceived, txId, slot);

                return Results.Ok();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing event");
                return Results.StatusCode(500);
            }
        });

        app.MapGet("/subscriptions/status", () => new
        {
            eventsReceived = _eventsReceived,
            lastEventTime = _lastEventTime == DateTimeOffset.MinValue ? null : _lastEventTime.ToString("o")
        });
    }
}
