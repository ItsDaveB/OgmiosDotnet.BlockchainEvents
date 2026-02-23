# Milestone 1 — Proof of Achievement

**Project:** OgmiosDotnet.BlockchainEvents  
**Repository:** [github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents)  
**Milestone:** 1 — Core Filtering Engine & Event Emission  
**Date:** February 2026  
**Commit:** `6438f2b` (main)

---

## Table of Contents

1. [Project Overview](#project-overview)
2. [Milestone Outputs](#milestone-outputs)
3. [Acceptance Criteria](#acceptance-criteria)
4. [Evidence of Completion](#evidence-of-completion)
5. [Demo Video](#demo-video)
6. [Repository Statistics](#repository-statistics)
7. [How to Run](#how-to-run)

---

## Project Overview

OgmiosDotnet.BlockchainEvents is a rule-based transaction filtering and event emission engine for Cardano. It connects to [Ogmios](https://ogmios.dev/) via the [OgmiosDotnet](https://github.com/ItsDaveB/OgmiosDotnet) SDK, applies configurable transaction rules in real-time, and emits standardized [CloudEvents 1.0](https://cloudevents.io/) via [Dapr](https://dapr.io/) pub/sub — enabling any downstream service (in any language) to subscribe to filtered blockchain events over HTTP.

### Architecture at a Glance

```
Ogmios (Cardano) ──▶ Chain Sync Adapter ──▶ Rule Engine ──▶ CloudEvent Factory ──▶ Dapr Pub/Sub ──▶ Subscribers
                                                                                          │
                                                                          Dapr State Store (checkpoint)
```

The system is composed of three .NET projects following clean architecture:

| Project                     | Purpose                                                                      |
| --------------------------- | ---------------------------------------------------------------------------- |
| **BlockchainEvents.Domain** | Core abstractions — rules interface, event models, checkpoint, configuration |
| **BlockchainEvents.Engine** | Rule engine and 5 built-in rule implementations                              |
| **BlockchainEvents.Worker** | .NET Worker service — chain sync, event emission, observability              |

---

## Milestone Outputs

### Output 1 — Open-Source Repository with Documented Codebase

The complete source code is publicly available under the MIT licence at:

> **https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents**

**Structure:**

```
src/
├── BlockchainEvents.Domain/       # 14 source files — models, abstractions, events
├── BlockchainEvents.Engine/       # 8 source files — rule engine + 5 built-in rules
└── BlockchainEvents.Worker/       # 16 source files — worker, services, extractors
tests/
└── BlockchainEvents.Tests/        # 10 test files — 59 unit tests, 100% pass rate
tools/
└── BlockchainEvents.DemoSubscriber/   # .NET subscriber demonstrating typed consumption
docs/
├── architecture.md                # System architecture and component design
├── event-schema.md                # CloudEvents specification with Cardano extensions
└── integration-guide.md           # Step-by-step guide for building custom rules
```

---

### Output 2 — Rule-Based Filtering Engine with 3+ Built-In Rules

The engine provides **5 built-in rules**, each inheriting from `TransactionRuleBase` and implementing the `ITransactionRule` interface:

| #   | Rule                       | File                                                          | Description                                                                                   |
| --- | -------------------------- | ------------------------------------------------------------- | --------------------------------------------------------------------------------------------- |
| 1   | **AddressMatchRule**       | `src/BlockchainEvents.Engine/Rules/AddressMatchRule.cs`       | Filters transactions by wallet address or address prefix                                      |
| 2   | **PolicyIdAssetRule**      | `src/BlockchainEvents.Engine/Rules/PolicyIdAssetRule.cs`      | Filters by minting policy ID and/or asset name                                                |
| 3   | **MetadataKeyValueRule**   | `src/BlockchainEvents.Engine/Rules/MetadataKeyValueRule.cs`   | Filters by metadata label (e.g. CIP-20 label 674) and key/value patterns                      |
| 4   | **GovernanceTreasuryRule** | `src/BlockchainEvents.Engine/Rules/GovernanceTreasuryRule.cs` | Filters governance actions, treasury withdrawals, stake delegations, registrations, and votes |
| 5   | **AllTransactionsRule**    | `src/BlockchainEvents.Engine/Rules/AllTransactionsRule.cs`    | Matches all transactions for testing and full capture scenarios                               |

All rules implement `ITransactionRule` with a two-phase evaluation — fast `IsMatch()` followed by `Evaluate()` only on match.

---

### Output 3 — CloudEvents 1.0 Event Schema with Cardano Extensions

All emitted events conform to the [CloudEvents 1.0 specification](https://cloudevents.io/) with custom Cardano extension attributes:

```json
{
  "specversion": "1.0",
  "id": "tx-8a3b2c1d-address-match-1705312200000",
  "source": "cardano://mainnet/slot/115545883/block/4e58bb36...",
  "type": "io.cardano.transaction.address-match",
  "subject": "Address Match",
  "time": "2024-01-15T10:30:00.000Z",
  "datacontenttype": "application/json",
  "cardanoslot": 115545883,
  "cardanoblock": "4e58bb36...",
  "cardanoblockheight": 10842567,
  "cardanoera": "Conway",
  "cardanonetwork": "mainnet",
  "data": {
    "transactionId": "8a3b2c1d...",
    "ruleId": "address-match",
    "ruleName": "Address Match",
    "matchedCriteria": { "matched_addresses": ["addr1qx..."] },
    "transaction": { ... }
  }
}
```

Full specification: [`docs/event-schema.md`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/blob/main/docs/event-schema.md)

---

### Output 4 — Unit Test Suite

The test project contains **59 unit tests** across **10 test classes**, all passing:

| Test Class                    | Tests | Covers                                                                        |
| ----------------------------- | ----- | ----------------------------------------------------------------------------- |
| `AddressMatchRuleTests`       | 5     | Address and prefix matching, disabled state                                   |
| `PolicyIdAssetRuleTests`      | 5     | Policy ID, asset name matching, disabled state                                |
| `MetadataKeyValueRuleTests`   | 11    | Label, key pattern, value pattern matching, MatchAny mode, empty metadata     |
| `GovernanceTreasuryRuleTests` | 9     | Governance actions, treasury, delegation, registration, votes, combined flags |
| `AllTransactionsRuleTests`    | 5     | Always-match behaviour, enabled/disabled, default options, evaluate output    |
| `RuleEngineTests`             | 7     | Multi-rule evaluation, disabled rules, empty rule set                         |
| `BlockchainEventFactoryTests` | 5     | CloudEvent creation, unique IDs, Cardano extensions                           |
| `SyncCheckpointTests`         | 6     | Serialization round-trip, equality, camelCase                                 |
| `DaprCheckpointServiceTests`  | 6     | Get/save/delete with ETag concurrency                                         |

**Test run output:**

```
Test Run Successful.
Total tests: 59
     Passed: 59
 Total time: 0.97 Seconds
```

---

### Output 5 — Developer Documentation

| Document                                                                                                                     | Purpose                                                                                   |
| ---------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------- |
| [`docs/architecture.md`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/blob/main/docs/architecture.md)           | System architecture, component design, data flow, deployment, performance characteristics |
| [`docs/event-schema.md`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/blob/main/docs/event-schema.md)           | Complete CloudEvents schema specification, field definitions, examples                    |
| [`docs/integration-guide.md`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/blob/main/docs/integration-guide.md) | Step-by-step guide for building custom rules with examples                                |
| [`README.md`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/blob/main/README.md)                                 | Project overview, quick start, configuration reference                                    |
| [`CONTRIBUTING.md`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/blob/main/CONTRIBUTING.md)                     | Development setup, project structure, adding new rules                                    |
| [`postman/`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/tree/main/postman)                                    | Postman collection and environment for API testing                                        |

---

### Output 6 — CI/CD Pipeline

Three GitHub Actions workflows provide automated quality gates:

| Workflow                    | Trigger                             | Purpose                                                                                        |
| --------------------------- | ----------------------------------- | ---------------------------------------------------------------------------------------------- |
| **CI** (`ci.yml`)           | Push/PR to `main`                   | Build → Test → Lint (format check) → Docker build                                              |
| **CodeQL** (`codeql.yml`)   | Push/PR to `main` + weekly schedule | Security analysis with `security-extended` and `security-and-quality` query suites             |
| **Release** (`release.yml`) | Tag push `v*`                       | Build → Test → Publish multi-arch Docker image (`amd64`/`arm64`) to `ghcr.io` → GitHub Release |

---

### Output 7 — Observability Stack

The project ships with a complete observability stack:

| Component           | Purpose                         | Port   |
| ------------------- | ------------------------------- | ------ |
| **Grafana**         | Dashboard visualization         | `4002` |
| **Prometheus**      | Metrics scraping (15s interval) | `4003` |
| **Zipkin**          | Distributed tracing             | `4004` |
| **Dapr Dashboard**  | Sidecar monitoring              | `4005` |
| **Redis Commander** | State store inspection          | `4006` |

**Custom Prometheus metrics** (9 metrics exposed at `/metrics`):

| Metric                         | Type      | Description                            |
| ------------------------------ | --------- | -------------------------------------- |
| `events_emitted_total`         | Counter   | Total events emitted, labelled by rule |
| `events_failed_total`          | Counter   | Failed event emissions                 |
| `processing_latency_seconds`   | Histogram | Block processing latency (p50/p90/p99) |
| `blocks_processed_total`       | Counter   | Total blocks processed                 |
| `last_processed_slot`          | Gauge     | Most recent slot processed             |
| `transactions_processed_total` | Counter   | Total transactions evaluated           |
| `events_in_flight`             | Gauge     | Events currently being published       |
| `enabled_rules_total`          | Gauge     | Number of enabled rules                |
| `rule_enabled`                 | Gauge     | Per-rule enabled status                |

**Grafana dashboard** — 23 data panels across 6 organised rows:

| Row                        | Panels                                                                                               |
| -------------------------- | ---------------------------------------------------------------------------------------------------- |
| Chain Sync Overview        | Current Slot, Blocks Processed, Transactions Processed, Events Emitted, Events Failed, Enabled Rules |
| Active Rules               | Minswap Swap Orders, Governance Votes, Metadata Transactions                                         |
| Throughput                 | Block Processing Rate, Event Emission Rate by Rule, Transaction Processing Rate                      |
| Latency & Reliability      | Processing Latency (p50/p90/p99), Events In-Flight, Events by Rule table                             |
| .NET Runtime               | CPU Usage, Memory Usage, GC Collections, ThreadPool & Timers                                         |
| Network & Connections      | Network I/O, Active Connections, Outbound HTTP Latency                                               |
| Dapr Sidecar _(collapsed)_ | Pub/Sub Rate, Pub/Sub Latency                                                                        |

---

## Acceptance Criteria

### 1. The filtering engine processes live Cardano mainnet transactions in real-time

The `BlockchainEventsWorker` connects to a hosted Ogmios node (via Demeter) pointing at Cardano mainnet and processes blocks at chain-tip speed with checkpoint-based resumption.

### 2. At least 3 distinct rule types are implemented and functional

**5 rule types** are implemented (exceeding the minimum of 3):

1. `AddressMatchRule` — wallet address / prefix filtering
2. `PolicyIdAssetRule` — minting policy and asset name filtering
3. `MetadataKeyValueRule` — transaction metadata label and pattern filtering
4. `GovernanceTreasuryRule` — Conway-era governance action filtering
5. `AllTransactionsRule` — catch-all for testing and full capture

Three rules are enabled in the current deployment:

- **AddressMatchRule** — Minswap V2 DEX batched swap orders (matched by order contract address prefix)
- **GovernanceTreasuryRule** — on-chain governance votes only (CIP-1694)
- **MetadataKeyValueRule** — any transaction carrying metadata (MatchAny mode)

### 3. Events are emitted as CloudEvents 1.0 with Cardano extension attributes

The `BlockchainEventFactory` produces CloudEvents 1.0 payloads with standard attributes plus Cardano extensions (`cardanoslot`, `cardanoblock`, `cardanoblockheight`, `cardanoera`, `cardanonetwork`). See [`docs/event-schema.md`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/blob/main/docs/event-schema.md).

### 4. Downstream services can subscribe to events without requiring the .NET SDK

Dapr delivers events as plain HTTP + JSON (CloudEvents), so any language can subscribe by exposing a POST endpoint. The included `tools/BlockchainEvents.DemoSubscriber/` demonstrates strongly-typed consumption in C#.

### 5. Unit tests pass with adequate coverage of rules, events, and checkpointing

**59 tests, 100% pass rate**, covering:

- All 5 built-in rules (AddressMatch, PolicyIdAsset, MetadataKeyValue, GovernanceTreasury, AllTransactions)
- MetadataKeyValueRule MatchAny mode
- Rule engine multi-rule evaluation logic
- CloudEvent factory output correctness
- Checkpoint serialization and concurrency
- Dapr state store interactions (mocked)

### 6. Developer documentation enables new contributors to build custom rules

[`docs/integration-guide.md`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/blob/main/docs/integration-guide.md) covers the `ITransactionRule` interface, step-by-step rule creation, `TransactionData`/`RuleContext` references, three worked examples (Whale Alert, Token Mint, CIP-25 NFT), and debugging tips.

### 7. The system is containerised and runs with a single `docker compose up` command

```bash
docker compose up --build
```

Brings up 11 containers: worker, Dapr sidecars, Redis, Prometheus, Grafana, Zipkin, and demo subscriber.

---

## Evidence of Completion

| #   | Evidence Item                                                   | Location                                                                                                                                      |
| --- | --------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | Public GitHub repository with full source code                  | [github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents)                                |
| 2   | 5 built-in rule implementations                                 | [`src/BlockchainEvents.Engine/Rules/`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/tree/main/src/BlockchainEvents.Engine/Rules) |
| 3   | CloudEvents event schema specification                          | [`docs/event-schema.md`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/blob/main/docs/event-schema.md)                            |
| 4   | 59 passing unit tests                                           | [`tests/BlockchainEvents.Tests/`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/tree/main/tests/BlockchainEvents.Tests)           |
| 5   | Architecture, event schema, and integration guide documentation | [`docs/`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/tree/main/docs)                                                           |
| 6   | CI/CD pipelines (build, security, release)                      | [`.github/workflows/`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/tree/main/.github/workflows)                                 |
| 7   | Demo video showing end-to-end operation                         | [See below](#demo-video)                                                                                                                      |

---

## Demo Video

> **Video link:** _(to be added after recording)_

---

## Repository Statistics

| Metric                      | Value              |
| --------------------------- | ------------------ |
| Source files (`.cs`)        | 38                 |
| Test files (`.cs`)          | 10                 |
| Source lines of code        | ~1,590             |
| Test lines of code          | ~1,500             |
| Documentation lines (docs/) | ~930               |
| Unit tests                  | 59 (100% passing)  |
| Built-in rules              | 5                  |
| Custom Prometheus metrics   | 9                  |
| Grafana dashboard panels    | 23 (across 6 rows) |
| Docker Compose services     | 11                 |
| CI/CD workflows             | 3                  |
| Licence                     | MIT                |

---

## How to Run

### Prerequisites

- Docker & Docker Compose
- Access to an Ogmios endpoint (or use the configured Demeter hosted node)

### Quick Start

```bash
git clone https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents.git
cd OgmiosDotnet.BlockchainEvents
./setup.sh                  # optional: adds blockchain.local hostname
docker compose up --build
```

### Access Points

All services on `localhost` (or `blockchain.local` if you ran `./setup.sh`):

| Service             | Port   |
| ------------------- | ------ |
| Worker health check | `4000` |
| Grafana dashboard   | `4002` |
| Prometheus          | `4003` |
| Zipkin traces       | `4004` |
| Dapr dashboard      | `4005` |
| Redis Commander     | `4006` |

### Run Tests

```bash
dotnet test
```
