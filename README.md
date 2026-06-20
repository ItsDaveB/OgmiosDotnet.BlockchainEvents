# OgmiosDotnet.BlockchainEvents

[![CI](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/actions/workflows/ci.yml/badge.svg)](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

Rule-based transaction filtering for Cardano. Connects to [Ogmios](https://ogmios.dev/), applies configurable rules, and emits [CloudEvents](https://cloudevents.io/) via Dapr pub/sub.

## Features

- Real-time chain sync via Ogmios
- Pluggable rule engine with built-in rules
- CloudEvents 1.0 output with Cardano extensions
- **Dual delivery**: HTTP (Dapr pub/sub) and gRPC server-streaming
- At-least-once delivery with configurable retry and dead letter queues
- Dapr state store checkpointing with ETag concurrency
- OpenAPI/Swagger API documentation
- SDK-less event consumption (any language)
- **React event viewer UI** with real-time SSE dashboard and rule filter selector

## Documentation

- [Architecture Overview](docs/architecture.md) - System design and components
- [Event Schema](docs/event-schema.md) - CloudEvents specification
- [Integration Guide](docs/integration-guide.md) - Building custom rules
- [UI Consumer Guide](docs/ui-consumer-guide.md) - Building and extending the React dashboard
- [Benchmark Results](docs/benchmarks.md) - Performance and delivery guarantees
- [OpenAPI Spec](docs/openapi.json) - API specification
- [Contributing](CONTRIBUTING.md) - Development guidelines

## Project Structure

The repository separates backend and frontend into independently deployable layers:

```
/backend  →  src/                  # .NET event delivery layer (Worker, Engine, Domain)
/ui       →  tools/event-viewer/  # React interactive consumer & visualisation

src/
├── BlockchainEvents.Domain/   # Models, abstractions, events
├── BlockchainEvents.Engine/   # Rule engine and implementations
└── BlockchainEvents.Worker/   # .NET Worker service (HTTP + gRPC + SSE)
tools/
├── event-viewer/              # React 19 dashboard (SSE consumer)
├── BlockchainEvents.DemoSubscriber/  # .NET pub/sub consumer demo
└── milestone-2-demo.sh        # Milestone 2 guided demo
protos/
└── blockchain_events.proto    # gRPC service definitions
tests/
└── BlockchainEvents.Tests/    # Unit tests (76 tests)
docs/
├── architecture.md            # System architecture
├── event-schema.md            # Event schema specification
├── integration-guide.md       # Custom rule development
├── ui-consumer-guide.md       # UI setup, architecture, extension guide
├── benchmarks.md              # Performance benchmarks
└── openapi.json               # OpenAPI 3.0 spec
```

## Built-in Rules

| Rule                   | Purpose                                    |
| ---------------------- | ------------------------------------------ |
| **AddressMatch**       | Filter by wallet addresses or prefixes     |
| **PolicyIdAsset**      | Filter by policy IDs and asset names       |
| **MetadataKeyValue**   | Filter by metadata labels and patterns     |
| **GovernanceTreasury** | Filter governance actions, votes, treasury |
| **AllTransactions**    | Match all transactions (testing/capture)   |

Rules are configured via `appsettings.json` or have sensible defaults.

## Configuration

Minimal config (`appsettings.json`):

```json
{
  "BlockchainEvents": {
    "Network": "preprod",
    "StateStoreName": "statestore",
    "PubSubName": "pubsub",
    "TopicName": "blockchain-events",
    "CheckpointKey": "sync-checkpoint"
  },
  "Ogmios": {
    "Connection": {
      "Host": "localhost",
      "Port": 1337
    }
  }
}
```

## Running

### Prerequisites

- .NET 10 SDK
- Docker (for full stack)
- Ogmios running locally (port 1337)

### First-Time Setup (optional)

```bash
./setup.sh          # adds blockchain.local to /etc/hosts — use instead of localhost
```

### Docker Compose (full stack)

```bash
docker compose up --build

# View traces at http://localhost:4004
```

### Local Development

```bash
# Terminal 1: Start infrastructure
docker compose up redis placement zipkin

# Terminal 2: Run with Dapr CLI
dapr run --app-id blockchain-events \
         --app-port 4000 \
         --resources-path ./dapr/components \
         --config ./dapr/config/config.yaml \
         -- dotnet run --project src/BlockchainEvents.Worker
```

### Tests

```bash
dotnet test
```

### gRPC Subscription

```bash
# Install grpcurl
brew install grpcurl

# Subscribe to all events (streams until Ctrl+C)
grpcurl -plaintext -import-path . -proto protos/blockchain_events.proto \
  -d '{}' localhost:4010 blockchain_events.BlockchainEventService/Subscribe

# Subscribe to address-match events only
grpcurl -plaintext -import-path . -proto protos/blockchain_events.proto \
  -d '{"rule_filter": "address-match"}' localhost:4010 blockchain_events.BlockchainEventService/Subscribe
```

### Guided Demo

```bash
# Milestone 2: event delivery layer (AC-1 through AC-3)
./tools/milestone-2-demo.sh

# Milestone 3: interactive UI consumer & visualisation
./tools/milestone-3-demo.sh
```

### Event Viewer UI

```bash
docker compose up --build
# Open http://localhost:4020 (all rules)
#      http://localhost:4021 (metadata filter)
#      http://localhost:4022 (governance filter)
#      http://localhost:4023 (address match filter)
```

See [docs/ui-consumer-guide.md](docs/ui-consumer-guide.md) for setup, architecture, and extension guide.

## Custom Rules

```csharp
public class HighFeeRule(IOptions<HighFeeOptions> options) : TransactionRuleBase
{
    public override string Id => "high-fee";
    public override string Name => "High Fee Rule";
    public override string Description => "Matches high-fee transactions";
    public override bool IsEnabled => options.Value.Enabled;

    public override bool IsMatch(TransactionData tx, RuleContext ctx)
        => tx.Fee > options.Value.ThresholdLovelace;

    public override RuleMatchResult Evaluate(TransactionData tx, RuleContext ctx)
        => new(Id, Name, new Dictionary<string, object> { ["fee"] = tx.Fee });
}

// Register: services.AddSingleton<ITransactionRule, HighFeeRule>();
```

See [docs/integration-guide.md](docs/integration-guide.md) for detailed examples.

## Event Schema

Events are emitted as CloudEvents 1.0 with Cardano extensions:

```json
{
  "specversion": "1.0",
  "id": "tx-abc123-address-match-1705312200000",
  "source": "cardano://mainnet/slot/115545883/block/4e58bb36...",
  "type": "io.cardano.transaction.address-match",
  "cardanoslot": 115545883,
  "cardanonetwork": "mainnet",
  "data": {
    "transactionId": "abc123...",
    "ruleId": "address-match",
    "matchedCriteria": { ... }
  }
}
```

See [docs/event-schema.md](docs/event-schema.md) for complete specification.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for development setup and guidelines.

## License

[MIT](LICENSE)
