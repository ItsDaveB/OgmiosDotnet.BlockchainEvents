# Milestone 2 — Proof of Achievement

**Project:** OgmiosDotnet.BlockchainEvents  
**Repository:** [github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents)  
**Milestone:** 2 — Event Delivery Layer  
**Date:** April 2026  
**Commit:** `0091279` (main)

---

## Overview

OgmiosDotnet.BlockchainEvents filters Cardano transactions in real-time and emits standardised [CloudEvents 1.0](https://cloudevents.io/) via **HTTP** (Redis Streams pub/sub) and **gRPC** (server-side streaming). Both protocols deliver identical payloads with Cardano extension attributes. Full architecture details in [`docs/architecture.md`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/blob/main/docs/architecture.md).

---

## Milestone Outputs

### Output 1 — Queue-Backed Delivery System

Filtered transactions are published via pub/sub backed by **Redis Streams** with at-least-once delivery, configurable retry policies, circuit breakers, and a dead letter queue. All delivery parameters are customisable via YAML with production-ready defaults — see [`pubsub.yaml`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/blob/main/dapr/components/pubsub.yaml) and [`resiliency.yaml`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/blob/main/dapr/components/resiliency.yaml).

Key defaults: max retries (3/10/20 across layers), exponential backoff (30 s max), circuit breaker (>8 failures, 45 s recovery), dead letter topic (`blockchain-events-dlq`), gRPC channel capacity (1,000 events, DropOldest).

### Output 2 — HTTP and gRPC Endpoints

| Protocol | Endpoint / RPC                | Description                                           |
| -------- | ----------------------------- | ----------------------------------------------------- |
| HTTP     | `POST /events/blockchain`     | Push delivery — JSON CloudEvents                      |
| HTTP     | `GET /events/stream`          | SSE for browser-based real-time consumption           |
| gRPC     | `Subscribe(SubscribeRequest)` | Server-streaming protobuf with optional `rule_filter` |
| gRPC     | `GetStatus(StatusRequest)`    | Active subscriber count, uptime                       |

Port 4000: HTTP/1.1 + HTTP/2. Port 4010: HTTP/2-only gRPC. Both deliver identical CloudEvents data from `BlockchainEventFactory.Create()`. Schema details in [`docs/event-schema.md`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/blob/main/docs/event-schema.md).

### Output 3 — At-Least-Once Delivery and Automatic Retry

Redis Streams consumer groups, multi-layer retry policies (outbound constant + inbound exponential), circuit breaker, dead letter queue, and checkpoint persistence with ETag concurrency. Verified by guided demo (Demos 4–5) in [`tools/milestone-2-demo.sh`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/blob/main/tools/milestone-2-demo.sh).

### Output 4 — API Documentation with Postman Examples

- **OpenAPI 3.0.1:** [`docs/openapi.json`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/blob/main/docs/openapi.json) + Swagger UI at `http://localhost:4000/swagger`
- **Postman:** [`postman/`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/tree/main/postman) — 8 folders, 20+ requests with local environment file
- **Guides:** [`docs/integration-guide.md`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/blob/main/docs/integration-guide.md)

### Output 5 — Docker Compose Local Setup

[`docker-compose.yml`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/blob/main/docker-compose.yml) starts **15 services**: worker (4000/4010), demo-subscriber (4001), Redis (6379), Prometheus (4003), Grafana (4002), Zipkin (4004), Redis Commander (4006), 4× event-viewer React UIs (4020–4023), plus sidecars and placement.

### Output 6 — Benchmark File

[`docs/benchmarks.md`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/blob/main/docs/benchmarks.md) — 7 scenarios covering throughput (~800–1,200 blocks/s normal, ~500 events/s burst), latency (p99 < 50 ms), recovery (< 3 s consumer, < 5 s worker), and a 24-hour soak test.

---

## Acceptance Criteria

### AC-1: Filtered transactions are enqueued and persisted; queue supports retry and at-least-once delivery

Matched transactions publish as CloudEvents to `blockchain-events` topic on Redis Streams. Configurable retry (constant + exponential), circuit breaker, and DLQ handle all failure modes. Demo: guided demo (Demo 4) — kill consumer → events queue → restart → 100% redelivery. Unit tests: `SaveCheckpointAsync_SavesCheckpointWithETag`, `SaveCheckpointAsync_ThrowsOnConcurrencyConflict`, `BroadcastAsync_DropsOldestWhenChannelFull`.

### AC-2: Consumers subscribe via HTTP and gRPC with identical payloads

HTTP receives JSON CloudEvents on `POST /events/blockchain`; gRPC streams protobuf via `Subscribe()` on port 4010. Both originate from `BlockchainEventFactory.Create()` — identical fields verified by test `Subscribe_MapsAllCloudEventsFields`. Evidence: Postman collection (20+ requests) + guided demo (Demo 3) gRPC verification via `grpcurl`.

### AC-3: Restarting publisher or consumer results in continued delivery without data loss

- **Consumer restart:** messages queue in Redis Streams, re-delivered on restart (< 3 s, 0 lost)
- **Worker restart:** checkpoint persists slot/block/height with ETag concurrency; resumes from saved slot (< 100 blocks re-processed, < 5 s)

Evidence: guided demo (Demo 4 — consumer crash/redelivery, Demo 5 — worker crash/checkpoint recovery)

### AC-4: API reference with all endpoints, parameters, response formats; Postman executes successfully

[`docs/openapi.json`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/blob/main/docs/openapi.json) (1,000+ lines), Swagger UI, [`postman/`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/tree/main/postman) with 20+ requests pre-configured for local Docker Compose.

### AC-5: docker-compose up runs all components and produces event flow

`docker compose up --build` starts 15 services. Verifiable via: worker logs (`Emitted N events`), Redis queue depth (`XLEN blockchain-events`), subscriber logs (`[Event] Type=io.cardano.transaction.address-match`), event viewers at `http://localhost:4020..4023`, Grafana dashboards at `http://localhost:4002`.

### AC-6: Benchmark file

[`docs/benchmarks.md`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/blob/main/docs/benchmarks.md) — throughput, latency, errors, and recovery across 7 scenarios. All measurements reference 9 custom Prometheus metrics.

---

## Evidence

**Demo video:** [https://youtu.be/VTBksbzPYXU](https://youtu.be/VTBksbzPYXU) — narrated walkthrough covering all six acceptance criteria (queue integration, HTTP + gRPC payloads, crash recovery, API docs, Docker Compose setup, benchmarks).

**Guided demo script** — [`tools/milestone-2-demo.sh`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/blob/main/tools/milestone-2-demo.sh) runs five live demonstrations covering AC-1 through AC-3 in a single session.

| #   | Requirement                               | Evidence                                                                                                                                                                                                                                                                                                                                                                                                                                                      |
| --- | ----------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | Queue integration + enqueue/consume demo  | [`DaprEventEmitter.cs`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/blob/main/src/BlockchainEvents.Worker/Services/DaprEventEmitter.cs), [`pubsub.yaml`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/blob/main/dapr/components/pubsub.yaml), [`resiliency.yaml`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/blob/main/dapr/components/resiliency.yaml), guided demo (Demo 2) — see [demo video](#demo-video) below |
| 2   | HTTP + gRPC live event payloads + OpenAPI | [`postman/`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/tree/main/postman) (20+ requests), [`docs/openapi.json`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/blob/main/docs/openapi.json), Swagger UI at `localhost:4000/swagger`, guided demo (Demo 3) — see [demo video](#demo-video) below                                                                                                                                    |
| 3   | Event redelivery + checkpoint recovery    | Guided demo (Demo 4 — consumer crash/redelivery, Demo 5 — worker crash/checkpoint recovery) in [`tools/milestone-2-demo.sh`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/blob/main/tools/milestone-2-demo.sh) — see [demo video](#demo-video) below                                                                                                                                                                                             |
| 4   | API docs + Postman collection             | [`docs/`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/tree/main/docs) (openapi.json, architecture.md, event-schema.md, integration-guide.md, benchmarks.md), [`postman/`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/tree/main/postman)                                                                                                                                                                                          |
| 5   | Docker Compose local setup                | [`docker-compose.yml`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/blob/main/docker-compose.yml) (15 services) — see [demo video](#demo-video) below                                                                                                                                                                                                                                                                                              |
| 6   | Benchmark file                            | [`docs/benchmarks.md`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/blob/main/docs/benchmarks.md) — 7 scenarios incl. gRPC streaming and 24-hour soak                                                                                                                                                                                                                                                                                            |

---

## How to Run

```bash
git clone https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents.git
cd OgmiosDotnet.BlockchainEvents
docker compose up --build          # 15 services — full event pipeline
dotnet test                        # 76 tests — 100% pass rate
./tools/milestone-2-demo.sh        # guided demo covering AC-1 through AC-5
```

| Service           | URL                             |
| ----------------- | ------------------------------- |
| Swagger UI        | `http://localhost:4000/swagger` |
| gRPC streaming    | `localhost:4010`                |
| Demo subscriber   | `http://localhost:4001/status`  |
| Grafana           | `http://localhost:4002`         |
| Event viewers 1–4 | `http://localhost:4020..4023`   |

---

## Demo Video

> **Video link:** [https://youtu.be/VTBksbzPYXU](https://youtu.be/VTBksbzPYXU)

Narrated walkthrough covering all six acceptance criteria: queue-backed delivery with Redis Streams, HTTP and gRPC event payloads, crash recovery with redelivery and checkpoint resume, API documentation with Postman, Docker Compose local setup (15 services), and benchmark results.
