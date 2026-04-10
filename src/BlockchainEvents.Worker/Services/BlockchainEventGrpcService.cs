using Grpc.Core;
using BlockchainEvents.Worker.Grpc;

namespace BlockchainEvents.Worker.Services;

/// <summary>
/// gRPC server-streaming service. Clients call Subscribe() and receive a continuous stream
/// of BlockchainEventMessage — the same payload that HTTP/Dapr subscribers receive.
/// </summary>
public sealed class BlockchainEventGrpcService(
    IEventBroadcaster broadcaster,
    ILogger<BlockchainEventGrpcService> logger) : BlockchainEventService.BlockchainEventServiceBase
{
    private static readonly DateTime StartTime = DateTime.UtcNow;

    public override async Task Subscribe(
        SubscribeRequest request,
        IServerStreamWriter<BlockchainEventMessage> responseStream,
        ServerCallContext context)
    {
        var ruleFilter = string.IsNullOrWhiteSpace(request.RuleFilter) ? null : request.RuleFilter;
        logger.LogInformation("gRPC subscriber connected (filter: {Filter})", ruleFilter ?? "all");

        using var subscription = broadcaster.Subscribe();

        try
        {
            while (!context.CancellationToken.IsCancellationRequested)
            {
                if (!await subscription.WaitToReadAsync(context.CancellationToken))
                    break;

                while (subscription.TryRead(out var cloudEvent))
                {
                    if (cloudEvent is null) continue;

                    // Apply optional rule filter
                    if (ruleFilter is not null && cloudEvent.Data?.RuleId != ruleFilter)
                        continue;

                    var message = MapToGrpcMessage(cloudEvent);
                    await responseStream.WriteAsync(message, context.CancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("gRPC subscriber disconnected (cancelled)");
        }
    }

    public override Task<StatusResponse> GetStatus(StatusRequest request, ServerCallContext context)
    {
        var uptime = DateTime.UtcNow - StartTime;
        return Task.FromResult(new StatusResponse
        {
            ActiveGrpcSubscribers = broadcaster.SubscriberCount,
            Uptime = uptime.ToString(@"d\.hh\:mm\:ss")
        });
    }

    private static BlockchainEventMessage MapToGrpcMessage(BlockchainEvent<TransactionMatchedData> e)
    {
        var msg = new BlockchainEventMessage
        {
            SpecVersion = e.SpecVersion,
            Id = e.Id,
            Source = e.Source,
            Type = e.Type,
            Subject = e.Subject ?? "",
            Time = e.Time.ToString("o"),
            DataContentType = e.DataContentType,
            DataSchema = e.DataSchema ?? "",
            CardanoSlot = e.CardanoSlot,
            CardanoBlock = e.CardanoBlock ?? "",
            CardanoBlockHeight = e.CardanoBlockHeight,
            CardanoEra = e.CardanoEra ?? "",
            CardanoNetwork = e.CardanoNetwork ?? ""
        };

        if (e.Data is not null)
        {
            var payload = new TransactionMatchedPayload
            {
                TransactionId = e.Data.TransactionId,
                Slot = e.Data.Slot,
                BlockHeight = e.Data.BlockHeight,
                BlockHash = e.Data.BlockHash,
                RuleId = e.Data.RuleId,
                RuleName = e.Data.RuleName
            };

            // Flatten matched criteria to string map
            foreach (var kv in e.Data.MatchedCriteria)
                payload.MatchedCriteria[kv.Key] = kv.Value?.ToString() ?? "";

            if (e.Data.Transaction is not null)
            {
                var tx = e.Data.Transaction;
                payload.Transaction = new TransactionPayload
                {
                    Id = tx.Id,
                    Slot = tx.Slot,
                    BlockHash = tx.BlockHash,
                    BlockHeight = tx.BlockHeight,
                    Index = tx.Index,
                    Fee = tx.Fee,
                    HasGovernanceAction = tx.HasGovernanceAction,
                    HasTreasuryWithdrawal = tx.HasTreasuryWithdrawal,
                    HasStakeDelegation = tx.HasStakeDelegation,
                    HasStakeRegistration = tx.HasStakeRegistration,
                    HasVote = tx.HasVote
                };
                payload.Transaction.InputAddresses.AddRange(tx.InputAddresses);
                payload.Transaction.OutputAddresses.AddRange(tx.OutputAddresses);
            }

            msg.Data = payload;
        }

        return msg;
    }
}
