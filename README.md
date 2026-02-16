# OgmiosDotnet.BlockchainEvents

[![CI](https://github.com/your-org/OgmiosDotnet.BlockchainEvents/actions/workflows/ci.yml/badge.svg)](https://github.com/your-org/OgmiosDotnet.BlockchainEvents/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

Rule-based transaction filtering for Cardano. Connects to [Ogmios](https://ogmios.dev/), applies configurable rules, and emits [CloudEvents](https://cloudevents.io/) via Dapr pub/sub.

## Features

- Real-time chain sync via Ogmios
- Pluggable rule engine with built-in rules
- CloudEvents 1.0 output with Cardano extensions
- Dapr state store checkpointing with ETag concurrency
- SDK-less event consumption (any language)

## Documentation

- [Architecture Overview](docs/architecture.md) - System design and components
- [Event Schema](docs/event-schema.md) - CloudEvents specification
- [Integration Guide](docs/integration-guide.md) - Building custom rules
- [Contributing](CONTRIBUTING.md) - Development guidelines

## Project Structure

```
src/
├── BlockchainEvents.Domain/   # Models, abstractions, events
├── BlockchainEvents.Engine/   # Rule engine and implementations
└── BlockchainEvents.Worker/   # .NET Worker service
tests/
└── BlockchainEvents.Tests/    # Unit tests
docs/
├── architecture.md            # System architecture
├── event-schema.md            # Event schema specification
└── integration-guide.md       # Custom rule development
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
    "Host": "localhost",
    "Port": 1337
  }
}
```

## Running

### Prerequisites

- .NET 9 SDK
- Docker (for full stack)
- Ogmios running locally (port 1337)

### Docker Compose (full stack)

```bash
docker compose up --build

# View traces at http://localhost:9411
```

### Local Development

```bash
# Terminal 1: Start infrastructure
docker compose up redis placement zipkin

# Terminal 2: Run with Dapr CLI
dapr run --app-id blockchain-events \
         --app-port 5000 \
         --resources-path ./dapr/components \
         --config ./dapr/config/config.yaml \
         -- dotnet run --project src/BlockchainEvents.Worker
```

### Tests

```bash
dotnet test
```

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
