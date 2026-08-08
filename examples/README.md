# Example Configurations

Practical, end-to-end rule configurations for common Cardano filtering scenarios. Each example is a drop-in `appsettings.json` overlay that configures the built-in rule engine while keeping Ogmios, Redis, and delivery settings intact.

Shipped with community release [`v1.0.1`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/releases/tag/v1.0.1). Milestone evidence: [`reports/milestone-4/`](../reports/milestone-4/).

| Example | Path | Demonstrates |
| ------- | ---- | ------------ |
| **Governance** | [`governance/`](governance/) | CIP-1694 governance actions and on-chain votes |
| **Treasury** | [`treasury/`](treasury/) | Treasury withdrawals (optionally with governance actions) |
| **Metadata** | [`metadata/`](metadata/) | CIP-20 / labelled metadata filtering (label 674 and MatchAny) |

## Prerequisites

- Docker & Docker Compose
- Access to an Ogmios endpoint (Demeter hosted or local)
- Repository cloned and dependencies available via `docker compose`

Update the `Ogmios` section in the chosen example (or mount your own secrets) before pointing at a live node.

## Quick Start

From the repository root:

```bash
# Governance filtering (actions + votes)
./examples/run-example.sh governance

# Treasury withdrawals
./examples/run-example.sh treasury

# Metadata label / pattern filtering
./examples/run-example.sh metadata
```

The helper script temporarily overlays the example `appsettings.json` onto the worker and starts the full stack (`docker compose up --build`).

### Manual overlay

```bash
WORKER_APPSETTINGS=./examples/governance/appsettings.json docker compose up --build
```

`WORKER_APPSETTINGS` is read by [`docker-compose.yml`](../docker-compose.yml) and mounts the chosen file into the worker container. Prefer `./examples/run-example.sh` so the path is validated first.

## Verifying End-to-End

After startup:

| Check | Where |
| ----- | ----- |
| Worker health | `http://localhost:4000/health` |
| Live events (SSE) | `http://localhost:4000/events/stream` |
| Event viewer | `http://localhost:4020` (all) / `4021` (metadata) / `4022` (governance) |
| Grafana | `http://localhost:4002` |
| Worker logs | Look for `Emitted N events` and rule IDs |

Expected rule IDs per example:

| Example | Primary rule ID | Typical event type |
| ------- | --------------- | ------------------ |
| Governance | `governance-treasury` | `io.cardano.transaction.governance-treasury` |
| Treasury | `governance-treasury` | `io.cardano.transaction.governance-treasury` |
| Metadata | `metadata-key-value` | `io.cardano.transaction.metadata-key-value` |

## Restoring Defaults

Example runs use `WORKER_APPSETTINGS` and do not modify the default worker file. The default configuration (Minswap address match + governance votes + metadata MatchAny + all-transactions) remains in [`src/BlockchainEvents.Worker/appsettings.json`](../src/BlockchainEvents.Worker/appsettings.json). Start without the env var (or omit `run-example.sh`) to use defaults.

## Extending

See [`docs/integration-guide.md`](../docs/integration-guide.md) for custom rules, and [`docs/getting-started.md`](../docs/getting-started.md) for full onboarding.
