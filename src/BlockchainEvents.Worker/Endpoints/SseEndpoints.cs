namespace BlockchainEvents.Worker.Endpoints;

public static class SseEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public static void MapSseEndpoints(this WebApplication app)
    {
        app.MapGet("/events/stream", async (
            HttpContext context,
            IEventBroadcaster broadcaster,
            ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("SseEndpoints");
            var ruleFilter = context.Request.Query.TryGetValue("ruleFilter", out var filterValues)
                && !string.IsNullOrWhiteSpace(filterValues.ToString())
                    ? filterValues.ToString()
                    : null;

            logger.LogInformation(
                "SSE subscriber connected from {RemoteIp} (filter: {Filter})",
                context.Connection.RemoteIpAddress,
                ruleFilter ?? "all");

            context.Response.ContentType = "text/event-stream";
            context.Response.Headers.CacheControl = "no-cache";
            context.Response.Headers.Connection = "keep-alive";
            await context.Response.Body.FlushAsync();

            using var subscription = broadcaster.Subscribe();
            var ct = context.RequestAborted;

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    if (!await subscription.WaitToReadAsync(ct))
                        break;

                    while (subscription.TryRead(out var cloudEvent))
                    {
                        if (cloudEvent is null) continue;

                        if (ruleFilter is not null && cloudEvent.Data?.RuleId != ruleFilter)
                            continue;

                        var payload = new
                        {
                            id = cloudEvent.Id,
                            type = cloudEvent.Type,
                            source = cloudEvent.Source,
                            time = cloudEvent.Time,
                            subject = cloudEvent.Subject,
                            cardanoSlot = cloudEvent.CardanoSlot,
                            cardanoBlockHeight = cloudEvent.CardanoBlockHeight,
                            cardanoBlock = cloudEvent.CardanoBlock,
                            cardanoEra = cloudEvent.CardanoEra,
                            cardanoNetwork = cloudEvent.CardanoNetwork,
                            data = cloudEvent.Data is null ? null : new
                            {
                                transactionId = cloudEvent.Data.TransactionId,
                                slot = cloudEvent.Data.Slot,
                                blockHeight = cloudEvent.Data.BlockHeight,
                                blockHash = cloudEvent.Data.BlockHash,
                                ruleId = cloudEvent.Data.RuleId,
                                ruleName = cloudEvent.Data.RuleName,
                                matchedCriteria = cloudEvent.Data.MatchedCriteria,
                                transaction = cloudEvent.Data.Transaction is null ? null : new
                                {
                                    id = cloudEvent.Data.Transaction.Id,
                                    fee = cloudEvent.Data.Transaction.Fee,
                                    inputAddresses = cloudEvent.Data.Transaction.InputAddresses,
                                    outputAddresses = cloudEvent.Data.Transaction.OutputAddresses,
                                    minswapSwap = cloudEvent.Data.Transaction.MinswapSwap is null ? null : new
                                    {
                                        dex = cloudEvent.Data.Transaction.MinswapSwap.Dex,
                                        direction = cloudEvent.Data.Transaction.MinswapSwap.Direction,
                                        orderType = cloudEvent.Data.Transaction.MinswapSwap.OrderType,
                                        swapInTicker = cloudEvent.Data.Transaction.MinswapSwap.SwapInTicker,
                                        swapOutTicker = cloudEvent.Data.Transaction.MinswapSwap.SwapOutTicker,
                                        amountIn = cloudEvent.Data.Transaction.MinswapSwap.AmountInDisplay,
                                        minReceive = cloudEvent.Data.Transaction.MinswapSwap.MinReceiveDisplay,
                                        batcherFeeAda = cloudEvent.Data.Transaction.MinswapSwap.BatcherFeeAda,
                                        datumSource = cloudEvent.Data.Transaction.MinswapSwap.DatumSource
                                    }
                                }
                            }
                        };

                        var json = JsonSerializer.Serialize(payload, JsonOptions);
                        await context.Response.WriteAsync($"data: {json}\n\n", ct);
                        await context.Response.Body.FlushAsync(ct);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("SSE subscriber disconnected (cancelled, filter: {Filter})", ruleFilter ?? "all");
            }
        })
        .WithName("EventStream")
        .WithTags("Streaming")
        .WithDescription("Server-Sent Events stream of blockchain events with optional ruleFilter query parameter");
    }
}
