# Getting Started — Community Onboarding

This guide helps developers **run**, **extend**, and **adopt** OgmiosDotnet.BlockchainEvents independently.

## What You Get

- Real-time Cardano transaction filtering via [Ogmios](https://ogmios.dev/)
- Pluggable rules (address, metadata, governance/treasury, policy/asset, catch-all)
- CloudEvents 1.0 delivery over HTTP (Dapr/Redis Streams), gRPC streaming, and SSE
- React event viewer for live visualisation
- Observability (Prometheus, Grafana, Zipkin)
- NuGet libraries for embedding the rule engine in your own apps
- Published Docker images on GitHub Container Registry (GHCR)

## 1. Run in Five Minutes

### Prerequisites

- Docker & Docker Compose
- An Ogmios endpoint (local node or hosted, e.g. Demeter)
- Optional: .NET 10 SDK (for local development and tests)

### Start the full stack

```bash
git clone https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents.git
cd OgmiosDotnet.BlockchainEvents
./setup.sh                  # optional: adds blockchain.local hostname
# Edit src/BlockchainEvents.Worker/appsettings.json → Ogmios.Connection
docker compose up --build   # multi-stage Dockerfiles compile inside the build — no host publish/ needed
```

Worker and DemoSubscriber `Dockerfile.local` files run `dotnet restore` / `dotnet publish` in-container, so a fresh clone does not need a host `publish/` directory. After pulling code changes, re-run `docker compose up --build` (add `--force-recreate` if an old container is still running stale bits).

On Apple Silicon, the Worker image install uses the distro `protoc` during the Docker build to avoid a known Grpc.Tools `linux_arm64` crash.

| Service | URL |
| ------- | --- |
| Worker health | http://localhost:4000/health |
| Swagger | http://localhost:4000/swagger |
| Event viewer | http://localhost:4020 |
| Grafana | http://localhost:4002 (admin / admin) |
| gRPC | localhost:4010 |

### Try an example filter profile

```bash
./examples/run-example.sh governance   # CIP-1694 actions + votes
./examples/run-example.sh treasury     # treasury withdrawals
./examples/run-example.sh metadata     # CIP-20 label 674
```

Details: [`examples/README.md`](../examples/README.md)

## 2. Consume Events (Any Language)

### HTTP / SSE

```bash
curl -N "http://localhost:4000/events/stream?ruleFilter=metadata-key-value"
```

### gRPC

```bash
grpcurl -plaintext -import-path . -proto protos/blockchain_events.proto \
  -d '{"rule_filter": "governance-treasury"}' \
  localhost:4010 blockchain_events.BlockchainEventService/Subscribe
```

### NuGet (embed the engine)

```bash
dotnet add package OgmiosDotnet.BlockchainEvents.Domain --version 1.0.1
dotnet add package OgmiosDotnet.BlockchainEvents.Engine --version 1.0.1
```

Published packages:

- [OgmiosDotnet.BlockchainEvents.Domain 1.0.1](https://www.nuget.org/packages/OgmiosDotnet.BlockchainEvents.Domain/1.0.1)
- [OgmiosDotnet.BlockchainEvents.Engine 1.0.1](https://www.nuget.org/packages/OgmiosDotnet.BlockchainEvents.Engine/1.0.1)

Releases use [Trusted Publishing](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing) (OIDC; no long-lived API key) and also publish to [GitHub Packages](https://github.com/ItsDaveB?tab=packages). Container images: `ghcr.io/itsdaveb/ogmiosdotnet.blockchainevents:1.0.1` and `.../event-viewer:1.0.1` ([GitHub Release v1.0.1](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/releases/tag/v1.0.1)).

## 3. Extend with a Custom Rule

1. Implement `ITransactionRule` (or inherit `TransactionRuleBase`) in `BlockchainEvents.Engine`
2. Add options under `Rules:YourRule` in `appsettings.json`
3. Register in `Program.cs`: `services.AddSingleton<ITransactionRule, YourRule>()`
4. Add unit tests under `tests/BlockchainEvents.Tests/Rules/`

Full walkthrough with worked examples: [`docs/integration-guide.md`](integration-guide.md)

## 4. Adopt in Production

| Concern | Recommendation |
| ------- | -------------- |
| Checkpointing | Redis state store with ETag concurrency (default) |
| Delivery | Redis Streams + retry/DLQ via Dapr resiliency |
| Scaling consumers | Multiple SSE/gRPC subscribers; independent event-viewer replicas |
| Config | Mount example or custom `appsettings.json` via `WORKER_APPSETTINGS` |
| Images | Pin `ghcr.io/itsdaveb/ogmiosdotnet.blockchainevents:1.0.1` (or later semver) |
| Security | CodeQL in CI; keep Ogmios credentials out of git (env / secrets) |

Architecture deep dive: [`docs/architecture.md`](architecture.md)  
Benchmarks: [`docs/benchmarks.md`](benchmarks.md)  
UI extension: [`docs/ui-consumer-guide.md`](ui-consumer-guide.md)

## 5. Quality Gates

```bash
dotnet test
dotnet format --verify-no-changes
```

CI runs build, tests, format check, CodeQL, and Docker builds on every push to `main`. Releases (`v*` tags) publish `linux/amd64` images to GHCR, NuGet packages to nuget.org + GitHub Packages, and a GitHub Release.

## 6. Where to Look Next

| Resource | Purpose |
| -------- | ------- |
| [`README.md`](../README.md) | Project overview |
| [`CONTRIBUTING.md`](../CONTRIBUTING.md) | Local development conventions |
| [`examples/`](../examples/) | Governance / treasury / metadata configs |
| [`reports/`](../reports/) | Milestone proofs and close-out report |
| [`docs/event-schema.md`](event-schema.md) | CloudEvents schema |
| Postman collection | [`postman/`](../postman/) |

## Licence

MIT — see [`LICENSE`](../LICENSE).
