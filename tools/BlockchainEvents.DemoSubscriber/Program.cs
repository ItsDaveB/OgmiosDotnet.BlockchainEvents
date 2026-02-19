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

app.MapGet("/dapr/subscribe", () => Results.Json(new[]
{
    new { pubsubname = "pubsub", topic = "blockchain-events", route = "/events/blockchain" }
}));

app.MapPost("/events/blockchain", async (HttpContext context) =>
{
    try
    {
        var envelope = await JsonSerializer.DeserializeAsync<JsonElement>(
            context.Request.Body, jsonOptions);

        JsonElement eventData = envelope.TryGetProperty("data", out var data) ? data : envelope;

        var txId = eventData.TryGetProperty("data", out var innerData)
            && innerData.TryGetProperty("transactionId", out var txIdProp)
            ? txIdProp.GetString()
            : "unknown";

        var slot = eventData.TryGetProperty("cardanoSlot", out var slotProp)
            ? slotProp.GetInt64()
            : 0;

        var blockHeight = eventData.TryGetProperty("cardanoBlockHeight", out var heightProp)
            ? heightProp.GetInt64()
            : 0;

        var network = eventData.TryGetProperty("cardanoNetwork", out var netProp)
            ? netProp.GetString()
            : "unknown";

        var ruleName = eventData.TryGetProperty("data", out var d2)
            && d2.TryGetProperty("ruleName", out var ruleNameProp)
            ? ruleNameProp.GetString()
            : "unknown";

        var count = Interlocked.Increment(ref eventsReceived);

        logger.LogInformation(
            "📥 Event #{Count} | {Network} | Slot {Slot} | Block {BlockHeight} | Rule: {Rule} | Tx: {TxId}",
            count, network, slot, blockHeight, ruleName, txId);

        if (eventData.TryGetProperty("data", out var d3)
            && d3.TryGetProperty("matchedCriteria", out var criteria))
        {
            logger.LogInformation("   🔍 Matched: {Criteria}", criteria.GetRawText());
        }

        return Results.Ok(new { status = "SUCCESS" });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "❌ Error processing event");
        return Results.Ok(new { status = "DROP" });
    }
});

app.MapGet("/status", () => new
{
    service = "demo-subscriber",
    eventsReceived,
    uptime = (DateTime.UtcNow - System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime()).ToString()
});

logger.LogInformation("🚀 Demo Subscriber starting — listening for blockchain events on /events/blockchain");
app.Run();
