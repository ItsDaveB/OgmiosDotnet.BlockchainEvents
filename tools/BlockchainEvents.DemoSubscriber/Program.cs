var builder = WebApplication.CreateBuilder(args);
builder.Logging.SetMinimumLevel(LogLevel.Information);

var app = builder.Build();
var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("DemoSubscriber");

var eventsReceived = 0L;

var jsonOptions = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
};

app.MapGet("/dapr/subscribe", () =>
{
    var subscriptions = new[]
    {
        new { pubsubname = "pubsub", topic = "blockchain-events", route = "/events/blockchain" }
    };

    logger.LogInformation("Registering {Count} subscription(s):", subscriptions.Length);
    foreach (var sub in subscriptions)
        logger.LogInformation("  ↳ Subscribed to topic '{Topic}', delivering to route '{Route}'",
            sub.topic, sub.route);

    return Results.Json(subscriptions);
});

app.MapPost("/events/blockchain", async (HttpContext context) =>
{
    try
    {
        var envelope = await JsonSerializer.DeserializeAsync<JsonElement>(
            context.Request.Body, jsonOptions);

        var blockchainEvent = envelope.TryGetProperty("data", out var dataElement)
            ? dataElement.Deserialize<BlockchainEvent<TransactionMatchedData>>(jsonOptions)
            : envelope.Deserialize<BlockchainEvent<TransactionMatchedData>>(jsonOptions);

        if (blockchainEvent is null)
        {
            logger.LogWarning("Received null event payload — skipping");
            return Results.Ok(new { status = "DROP" });
        }

        var count = Interlocked.Increment(ref eventsReceived);
        var matched = blockchainEvent.Data;

        logger.LogInformation("Received event #{Count} from topic 'blockchain-events'", count);
        logger.LogInformation("  Event Type : {EventType}", blockchainEvent.Type);
        logger.LogInformation("  Event Id   : {EventId}", blockchainEvent.Id);
        logger.LogInformation("  Network    : {Network}", blockchainEvent.CardanoNetwork);
        logger.LogInformation("  Era        : {Era}", blockchainEvent.CardanoEra);
        logger.LogInformation("  Slot       : {Slot}", blockchainEvent.CardanoSlot);
        logger.LogInformation("  Block      : {BlockHeight}", blockchainEvent.CardanoBlockHeight);
        logger.LogInformation("  Rule       : {RuleName} ({RuleId})", matched.RuleName, matched.RuleId);
        logger.LogInformation("  Transaction: {TxId}", matched.TransactionId);
        logger.LogInformation("  Fee        : {Fee} lovelace", matched.Transaction.Fee);

        if (matched.MatchedCriteria.Count > 0)
        {
            logger.LogInformation("  Matched criteria: {Criteria}",
                JsonSerializer.Serialize(matched.MatchedCriteria, jsonOptions));
        }

        logger.LogInformation("Acknowledged event #{Count} with status SUCCESS", count);
        return Results.Ok(new { status = "SUCCESS" });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to process event — acknowledging with status DROP to prevent redelivery");
        return Results.Ok(new { status = "DROP" });
    }
});

app.MapGet("/status", () => new
{
    service = "demo-subscriber",
    eventsReceived,
    uptime = (DateTime.UtcNow - System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime()).ToString()
});

logger.LogInformation("Demo Subscriber starting on {Urls}", builder.Configuration["ASPNETCORE_URLS"] ?? "http://localhost:4001");
logger.LogInformation("Waiting for subscription registration via GET /dapr/subscribe ...");
app.Run();
