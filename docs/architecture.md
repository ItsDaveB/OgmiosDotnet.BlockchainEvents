# OgmiosDotnet.BlockchainEvents - Architecture Documentation

## Overview

OgmiosDotnet.BlockchainEvents is a rule-based transaction filtering and event emission engine for Cardano. It processes live blockchain data via Ogmios, applies configurable transaction rules, and emits standardized CloudEvents for downstream consumption.

## Use Case

The system addresses the need for real-time, filtered blockchain event streams. Rather than processing every transaction, applications can subscribe to specific events matching their criteria—such as transactions involving particular addresses, minting specific tokens, or containing governance actions.

**Key scenarios:**

- Wallet notifications for incoming/outgoing transactions
- NFT marketplace tracking specific policy IDs
- DeFi protocol monitoring for liquidity changes
- Governance dashboards tracking proposals and votes
- Analytics pipelines requiring filtered transaction data

## System Architecture

```
┌─────────────────────────────────────────────────────────────────────────┐
│                        OgmiosDotnet.BlockchainEvents                     │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────────────────┐   │
│  │   Ogmios     │───▶│  Chain Sync  │───▶│     Rule Engine          │   │
│  │  (Cardano)   │    │   Adapter    │    │  ┌────────────────────┐  │   │
│  └──────────────┘    └──────────────┘    │  │  AddressMatchRule  │  │   │
│                                          │  │  PolicyIdAssetRule │  │   │
│                                          │  │  MetadataKeyValue  │  │   │
│                                          │  │  GovernanceTreasury│  │   │
│                                          │  │  [Custom Rules...] │  │   │
│                                          │  └────────────────────┘  │   │
│                                          └────────────┬─────────────┘   │
│                                                       │                  │
│                                                       ▼                  │
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────────────────┐   │
│  │    Dapr      │◀───│   Event      │◀───│   CloudEvent Factory     │   │
│  │   Pub/Sub    │    │   Emitter    │    │                          │   │
│  └──────────────┘    └──────┬───────┘    └──────────────────────────┘   │
│         │                   │                                           │
│         │            ┌──────▼───────┐                                   │
│         │            │   Event      │── In-memory fan-out (Channels)    │
│         │            │  Broadcaster │                                   │
│         │            └──────┬───────┘                                   │
│         │                   │                                           │
│         │            ┌──────▼───────┐                                   │
│         │            │  gRPC Stream │── Port 4010 (HTTP/2)              │
│         │            │   Service    │                                   │
│         │            └──────────────┘                                   │
│         │                                                                │
│         │            ┌──────────────┐                                   │
│         │            │  Checkpoint  │◀── Dapr State Store (Redis)       │
│         │            │   Service    │                                   │
│         │            └──────────────┘                                   │
└─────────┼───────────────────────────────────────────────────────────────┘
          │
          ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                         Downstream Consumers                             │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐                   │
│  │  HTTP/Dapr   │  │   gRPC       │  │   Service C  │                   │
│  │  (Any Lang)  │  │  (Streaming) │  │  (Any Lang)  │                   │
│  └──────────────┘  └──────────────┘  └──────────────┘                   │
└─────────────────────────────────────────────────────────────────────────┘
```

## Main Components

### 1. BlockchainEvents.Domain

Core abstractions and models shared across the system.

| Component            | Purpose                                     |
| -------------------- | ------------------------------------------- |
| `ITransactionRule`   | Interface for pluggable transaction filters |
| `TransactionData`    | Normalized transaction representation       |
| `RuleContext`        | Block context passed to rules               |
| `RuleMatchResult`    | Result of a successful rule match           |
| `SyncCheckpoint`     | Checkpoint state for resumption             |
| `BlockchainEvent<T>` | CloudEvents wrapper for emitted events      |

### 2. BlockchainEvents.Engine

Rule engine implementation and built-in rules.

| Component                | Purpose                                         |
| ------------------------ | ----------------------------------------------- |
| `RuleEngine`             | Evaluates transactions against registered rules |
| `TransactionRuleBase`    | Base class for rule implementations             |
| `AddressMatchRule`       | Filters by wallet addresses or prefixes         |
| `PolicyIdAssetRule`      | Filters by policy IDs and asset names           |
| `MetadataKeyValueRule`   | Filters by metadata labels and patterns         |
| `GovernanceTreasuryRule` | Filters governance actions and votes            |

### 3. BlockchainEvents.Worker

The runtime service connecting all components.

| Component                     | Purpose                                           |
| ----------------------------- | ------------------------------------------------- |
| `BlockchainEventsWorker`      | Background service orchestrating sync             |
| `OgmiosChainSyncAdapter`     | Bridges OgmiosDotnet to abstraction layer         |
| `DaprEventEmitter`            | Publishes CloudEvents via Dapr pub/sub            |
| `EventBroadcaster`            | In-memory fan-out to gRPC subscribers (Channels)  |
| `BlockchainEventGrpcService`  | gRPC server-streaming on port 4010 (HTTP/2)       |
| `DaprCheckpointService`       | Persists sync state with ETag concurrency         |
| `TransactionTransformer`      | Converts Ogmios types to TransactionData          |

## Data Flow

### Block Processing Flow

```
1. Ogmios WebSocket ──▶ Block received (praos/bft/ebb)
                           │
2. OgmiosChainSyncAdapter ─┴──▶ Transform to IBlockData
                                    │
3. BlockchainEventsWorker ──────────┴──▶ For each transaction:
                                              │
4. RuleEngine.MatchTransactions() ────────────┴──▶ Apply all enabled rules
                                                        │
5. For each match: ─────────────────────────────────────┤
   │                                                    │
   ├──▶ BlockchainEventFactory.Create() ──▶ CloudEvent │
   │                                                    │
   └──▶ DaprEventEmitter.EmitAsync() ──▶ Pub/Sub       │
              │                                         │
              └──▶ EventBroadcaster ──▶ gRPC Streams ──┘
                                              │
6. DaprCheckpointService.SaveCheckpointAsync() ◀───────┘
```

### Rollback Handling

```
1. Ogmios signals rollback to point P
2. BlockchainEventsWorker.OnRollbackAsync()
3. If P == origin: Delete checkpoint, reset counters
4. Else: Save checkpoint at rollback point
5. Chain sync continues from new position
```

## Rule Evaluation Logic

Rules are evaluated using a two-phase approach for efficiency:

```csharp
foreach (var rule in enabledRules)
{
    // Phase 1: Fast match check (cheap operation)
    if (rule.IsMatch(transaction, context))
    {
        // Phase 2: Detailed evaluation (only if matched)
        var result = rule.Evaluate(transaction, context);
        yield return result;
    }
}
```

**Key behaviors:**

- Rules are evaluated in registration order
- A transaction can match multiple rules (all matches emitted)
- Disabled rules are skipped entirely
- Exceptions in one rule don't affect others

## Checkpointing & Resumption

The system maintains sync state via Dapr state store with ETag-based optimistic concurrency:

```json
{
  "Slot": 115545883,
  "BlockHash": "4e58bb36837b32f894c8a57006e24b64c2d77bf4fc13b3b2c428fee8871e2491",
  "BlockHeight": 10842567,
  "TransactionsProcessed": 1250000,
  "EventsEmitted": 45000,
  "ProcessedAt": "2024-01-15T10:30:00Z"
}
```

**Checkpoint triggers:**

- Every 100 blocks (configurable)
- When new events are emitted
- On rollback

## Event Schema

Events are emitted as CloudEvents 1.0 with Cardano-specific extensions:

```json
{
  "specversion": "1.0",
  "id": "tx-abc123-address-match-1705312200000",
  "source": "cardano://mainnet/slot/115545883/block/4e58bb36...",
  "type": "io.cardano.transaction.address-match",
  "time": "2024-01-15T10:30:00Z",
  "datacontenttype": "application/json",
  "cardanoslot": 115545883,
  "cardanoblock": "4e58bb36...",
  "cardanoblockheight": 10842567,
  "cardanoera": "Conway",
  "cardanonetwork": "mainnet",
  "data": {
    "transactionId": "abc123...",
    "ruleId": "address-match",
    "ruleName": "Address Match",
    "matchedCriteria": { ... },
    "transaction": { ... }
  }
}
```

See [event-schema.md](event-schema.md) for complete specification.

## Deployment Architecture

### Docker Compose (Development/Demo)

```
┌─────────────────────────────────────────────────────────┐
│                    Docker Network                        │
│                                                          │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐   │
│  │    Redis     │  │  Placement   │  │    Zipkin    │   │
│  │  (streams)   │  │   (Dapr)     │  │  (tracing)   │   │
│  └──────────────┘  └──────────────┘  └──────────────┘   │
│                                                          │
│  ┌──────────────────────────────────────────────────┐   │
│  │              blockchain-events                    │   │
│  │  ┌──────────────┐     ┌──────────────────────┐   │   │
│  │  │    Worker    │◀───▶│    Dapr Sidecar      │   │   │
│  │  │   (App)      │     │ (pubsub, state, etc) │   │   │
│  │  └──────────────┘     └──────────────────────┘   │   │
│  └──────────────────────────────────────────────────┘   │
│                                                          │
│  ┌──────────────────────────────────────────────────┐   │
│  │             demo-subscriber (demo)                │   │
│  │  ┌──────────────┐     ┌──────────────────────┐   │   │
│  │  │  .NET / C#   │◀───▶│    Dapr Sidecar      │   │   │
│  │  │   (HTTP)     │     │                      │   │   │
│  │  └──────────────┘     └──────────────────────┘   │   │
│  └──────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────┘
```

### Production Considerations

- **Horizontal scaling**: Multiple worker instances with partitioned state
- **High availability**: Redis cluster or managed service
- **Observability**: Prometheus metrics, distributed tracing
- **Security**: TLS everywhere, secret management via Dapr

## Technology Stack

| Layer            | Technology                              |
| ---------------- | --------------------------------------- |
| Runtime          | .NET 10                                 |
| Chain Sync       | OgmiosDotnet 6.13.x                    |
| Event Bus        | Dapr Pub/Sub (Redis Streams)            |
| gRPC Streaming   | Grpc.AspNetCore 2.71.0 (HTTP/2)         |
| State Store      | Dapr State (Redis)                      |
| Serialization    | System.Text.Json / Protobuf 3.32.0      |
| UI Consumer      | React 19, Vite 6, TanStack Table        |
| API Documentation| OpenAPI 3.0 / Swagger (Swashbuckle)     |
| Testing          | xUnit, FluentAssertions, Moq            |
| Containerization | Docker, Docker Compose                  |
| CI/CD            | GitHub Actions                          |

## Performance Characteristics

- **Block processing**: ~1000 blocks/second (depends on transaction count)
- **Rule evaluation**: O(rules × transactions) per block
- **Memory footprint**: ~100MB base + transaction buffer
- **Checkpoint overhead**: <10ms per checkpoint operation

## Event Delivery Protocols

### HTTP (Dapr Pub/Sub)

Consumers receive events as CloudEvents JSON via HTTP POST. Any language can subscribe by exposing a POST endpoint — no SDK required. Delivery is at-least-once with configurable retry policies and dead letter queues.

### gRPC (Server-Side Streaming)

Consumers connect to port 4010 (HTTP/2) and receive a continuous stream of `BlockchainEventMessage` protobuf messages. The gRPC payload is identical to the HTTP payload, just serialised as protobuf.

```bash
# Subscribe to all events
grpcurl -plaintext -import-path . -proto protos/blockchain_events.proto \
  -d '{}' localhost:4010 blockchain_events.BlockchainEventService/Subscribe

# Subscribe with rule filter
grpcurl -plaintext -import-path . -proto protos/blockchain_events.proto \
  -d '{"rule_filter": "address-match"}' localhost:4010 blockchain_events.BlockchainEventService/Subscribe
```

The `EventBroadcaster` uses bounded `System.Threading.Channels` (capacity 1,000) with `DropOldest` back-pressure to protect the pipeline from slow consumers.

### SSE (Server-Sent Events)

Browser-based consumers connect via `GET /events/stream` on port 4000. The endpoint streams JSON CloudEvents as Server-Sent Events, with optional `ruleFilter` query parameter for rule-specific filtering (parity with gRPC).

```
GET /events/stream
GET /events/stream?ruleFilter=metadata-key-value
```

The React event viewer (`tools/event-viewer/`) is the reference SSE consumer. It demonstrates real-time visualisation, selectable rule filters, and connection status logging. See [docs/ui-consumer-guide.md](ui-consumer-guide.md) for setup and extension instructions.

### React Event Viewer (UI Consumer)

The UI layer (`tools/event-viewer/`) is a standalone React 19 application that communicates with the backend exclusively via the SSE API. It can be deployed independently (static nginx container) while the .NET worker runs separately.

| Component | Technology | Port |
| --------- | ---------- | ---- |
| Event Viewer | React 19, Vite, TanStack Table | 4020–4023 |
| Backend Worker | .NET 10, SSE + gRPC | 4000, 4010 |

Four Docker Compose instances demonstrate different rule filter configurations side-by-side, proving that consumers can be built independently with distinct filtering scenarios.

### API Documentation

OpenAPI/Swagger UI is available at `http://localhost:4000/swagger` when the worker is running. The static OpenAPI spec is at `docs/openapi.json`.

## Extensibility Points

1. **Custom Rules**: Implement `ITransactionRule` interface
2. **Event Enrichment**: Extend `BlockchainEventFactory`
3. **Alternative Pub/Sub**: Configure different Dapr component
4. **Custom State Store**: Configure different Dapr state provider
5. **Chain Source**: Implement `IChainSyncService` for non-Ogmios sources
6. **gRPC Consumers**: Connect to port 4010 for real-time streaming
