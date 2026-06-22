# Milestone 3 — Proof of Achievement

**Project:** OgmiosDotnet.BlockchainEvents  
**Repository:** [github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents)  
**Milestone:** 3 — Interactive Consumer & Visualisation  
**Date:** June 2026  
**Commit:** `b62ee61` (main)

---

## Overview

OgmiosDotnet.BlockchainEvents now includes a **React-based web application** that subscribes to the event delivery layer via Server-Sent Events (SSE) and provides real-time visualisation of filtered Cardano transactions. Filtered transactions are published to a message queue, consumed via HTTP and gRPC, and displayed on the dashboard as CloudEvents arrive — demonstrating end-to-end, event-driven consumption with plug-and-play custom rules.

The UI shows live transaction updates for each custom event filter type, with clear separation between backend and frontend layers for independent deployment. Full setup and extension instructions: [`docs/ui-consumer-guide.md`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/blob/main/docs/ui-consumer-guide.md)

---

## Milestone Outputs

### Output 1 — React-Based Web Application

A production-ready React 19 dashboard at [`tools/event-viewer/`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/tree/main/tools/event-viewer) that subscribes to the event delivery layer via SSE (`GET /events/stream`).

| Component | File | Purpose |
| --------- | ---- | ------- |
| Dashboard | `src/App.tsx` | Stats bar, sortable table, rule filter selector, connection log |
| SSE Hook | `src/useEventStream.ts` | EventSource consumer with auto-reconnect and connection logging |
| Rule Configs | `src/ruleConfigs.ts` | Demo filter configurations (metadata, governance, address) |
| Detail Drawer | `src/EventDetailDrawer.tsx` | Full CloudEvent payload inspection |
| Styling | `src/App.css` | Cardano-themed dark dashboard (1,500+ lines) |

Tech stack: React 19, Vite 6, TypeScript 5.8, TanStack React Table 8.21.

### Output 2 — Real-Time Visualisation of Filtered Transactions

The dashboard updates automatically as new filtered transactions arrive — events are consumed end-to-end from the delivery layer with instant, performant updates:

- **Live stats**: total events, events/sec sparkline, uptime, rule breakdown chips
- **Event table**: Time, Rule, Metadata, Transaction, Slot, Block, Fee, Outputs columns
- **Detail drawer**: inspect full CloudEvent payloads — transaction ID, slot, block height, matched rule, and complete transaction data
- **Pause/resume** and **clear** controls for demo management
- **500-event buffer** with newest-first display and animated row highlights

Each CloudEvent payload contains the transaction ID, slot number, block height, the matched rule, and the full transaction data.

### Output 3 — Example Filtering Scenarios

Four active rules are demonstrated as plug-and-play configurations, with sample events shown for each in the demo:

| Rule | Rule ID | Description | Filter / Instance |
| ---- | ------- | ----------- | ----------------- |
| **Address Match** | `address-match` | Minswap V2 DEX batched swap orders | Address Match button / http://localhost:4023 |
| **Metadata Match** | `metadata-key-value` | Transactions with specific metadata labels | Metadata button / http://localhost:4021 |
| **All Transactions** | `all-transactions` | Full capture of every transaction processed | All Rules / http://localhost:4020 |
| **Governance & Treasury** | `governance-treasury` | Governance votes and Conway-era actions (active; infrequent on mainnet) | Governance & Treasury button / http://localhost:4022 |

Each filter reconnects the SSE stream with `?ruleFilter=` and produces visually distinct results — different rule chips, metadata summaries, and event counts.

### Output 4 — Clear Backend/Frontend Separation

```
/backend  →  src/                  # .NET Worker (filtering, emission, APIs)
/ui       →  tools/event-viewer/  # React dashboard (SSE consumer only)
```

The frontend communicates with the backend **exclusively via exposed HTTP APIs** (SSE). No shared libraries, no embedded runtime, no direct database access. Each layer has its own Dockerfile and can be deployed independently:

| Layer | Docker Image | Port(s) |
| ----- | ------------ | ------- |
| Backend | `blockchain-events` | 4000 (HTTP/SSE), 4010 (gRPC) |
| Frontend | `event-viewer` (nginx) | 4020–4023 |

Custom rules can be created and plugged into the event pipeline independently of the UI layer.

### Output 5 — Developer Documentation

| Document | Purpose |
| -------- | ------- |
| [`docs/ui-consumer-guide.md`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/blob/main/docs/ui-consumer-guide.md) | Setup guide, architecture overview, API reference, extension sample code |
| [`tools/event-viewer/README.md`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/tree/main/tools/event-viewer/README.md) | Local development and build instructions |
| [`docs/architecture.md`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/blob/main/docs/architecture.md) | Updated with SSE and UI consumer sections |
| [`README.md`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/blob/main/README.md) | Updated project structure showing `/backend` and `/ui` modular design |

---

## Acceptance Criteria

### AC-1: Application builds and runs locally via docker-compose up; connection established to delivery layer; connection logs visible

The entire system runs through a single `docker compose up --build` command. Within seconds the worker begins syncing from the seeded checkpoint position, advancing multiple blocks. The UI connects to the delivery layer via SSE on port 4000. Connection status is shown in the header (Live/Connecting/Disconnected) and the **Connection Log** panel records connect/disconnect/reconnect events with timestamps. Worker logs record SSE subscriber connections:

```
info: SseEndpoints[0] SSE subscriber connected from ::ffff:172.18.0.1 (filter: metadata-key-value)
```

Evidence: [`docker-compose.yml`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/blob/main/docker-compose.yml), [`useEventStream.ts`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/blob/main/tools/event-viewer/src/useEventStream.ts), [`SseEndpoints.cs`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/blob/main/src/BlockchainEvents.Worker/Endpoints/SseEndpoints.cs) — see [demo video](#demo-video) below

### AC-2: UI updates automatically; at least 10 sample events displayed with metadata, timestamp, and rule type

Live CloudEvents payloads begin arriving within seconds of startup. Events populate the dashboard table in real time via `EventSource`, demonstrating end-to-end consumption from the message queue through HTTP delivery to the UI. Each row displays:

- **Timestamp:** formatted from CloudEvent `time` field (HH:MM:SS.mmm)
- **Rule type:** colour-coded chip from `data.ruleName`
- **Metadata:** summary from `data.matchedCriteria` (key/value patterns, labels)

The detail drawer allows inspection of the full matched criteria, CloudEvent envelope, and integrated data format on row click.

Evidence: [`App.tsx`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/blob/main/tools/event-viewer/src/App.tsx), [`EventDetailDrawer.tsx`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/blob/main/tools/event-viewer/src/EventDetailDrawer.tsx) — see [demo video](#demo-video) below

### AC-3: Two demo rule configurations selectable and produce distinct visual live results

The demo shows sample events for each of the four active plug-and-play rules, with selectable filter configurations via header buttons and pre-configured Docker instances:

1. **All Rules / All Transactions:** full capture of every matched transaction
2. **Metadata:** `metadata-key-value` rule — transactions with specific metadata labels
3. **Governance & Treasury:** `governance-treasury` rule — governance votes (infrequent on mainnet)
4. **Address Match:** `address-match` rule — Minswap V2 DEX transactions

Switching filters reconnects SSE with `?ruleFilter=` and clears the event buffer, producing immediately distinct rule chips and event counts in the stats bar.

Evidence: [`ruleConfigs.ts`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/blob/main/tools/event-viewer/src/ruleConfigs.ts), pre-configured viewer instances in [`docker-compose.yml`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/blob/main/docker-compose.yml) — see [demo video](#demo-video) below

### AC-4: Frontend communicates with backend only via exposed APIs; modular project structure

- Filtered transactions are published to the message queue and consumed via HTTP and gRPC on the backend; the UI consumes events exclusively via `EventSource` on `GET /events/stream`
- Backend at `src/`, frontend at `tools/event-viewer/`. Separate Dockerfiles and build pipelines
- Four independent viewer containers demonstrate multi-consumer deployment
- The entire implementation is customisable, including the ability to create custom rules and plug them into the event pipeline

Evidence: project structure in [`README.md`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/blob/main/README.md), SSE-only consumption in [`useEventStream.ts`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/blob/main/tools/event-viewer/src/useEventStream.ts)

### AC-5: Documentation includes setup guide, architecture overview, and sample code for extending the UI

[`docs/ui-consumer-guide.md`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/blob/main/docs/ui-consumer-guide.md) contains:

- Quick start (Docker Compose + local dev)
- Architecture diagram (backend ↔ frontend via SSE)
- API reference (`GET /events/stream`, `ruleFilter` parameter)
- Sample code: custom consumer hook, adding table columns, adding rule filters
- Deployment guide (UI-only CDN, backend-only Docker)
- Demo scenario descriptions

Verified by existence and cross-referenced in README and architecture docs. The demo references this proof document in [`reports/milestone-3/`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/tree/main/reports/milestone-3) and invites viewers to run the stack themselves using the setup instructions below.

---

## Evidence

**Demo video:** [https://youtu.be/uPpgb73ENp8](https://youtu.be/uPpgb73ENp8) — dedicated Milestone 3 narrated walkthrough covering filtered transactions published to the message queue, consumed via HTTP and gRPC, and real-time visualisation on the dashboard with live updates for each custom event filter type.

| #   | Requirement                                              | Evidence                                                                                                                                                                                                                                                                                                                                                                                                                                                        |
| --- | -------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | Public GitHub repo with UI project + Docker Compose      | [`tools/event-viewer/`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/tree/main/tools/event-viewer), [`docker-compose.yml`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/blob/main/docker-compose.yml) — see [demo video](#demo-video) below                                                                                                                                                                                            |
| 2   | Video demo showing live updates + filter type results    | [Demo Video](#demo-video) — [https://youtu.be/uPpgb73ENp8](https://youtu.be/uPpgb73ENp8): end-to-end event flow, dashboard live updates, sample events per rule type (Address Match, Metadata, All Transactions, Governance & Treasury)                                                                                                                                                                                                                          |
| 3   | README showing `/backend`, `/ui` modular structure       | [`README.md`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/blob/main/README.md) project structure section                                                                                                                                                                                                                                                                                                                                          |
| 4   | Documentation with setup and extension instructions      | [`docs/ui-consumer-guide.md`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/blob/main/docs/ui-consumer-guide.md), [`tools/event-viewer/README.md`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/tree/main/tools/event-viewer/README.md), [`reports/milestone-3/proof-of-achievement.md`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/blob/main/reports/milestone-3/proof-of-achievement.md)                          |
| 5   | SSE rule filtering (parity with gRPC)                    | [`SseEndpoints.cs`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/blob/main/src/BlockchainEvents.Worker/Endpoints/SseEndpoints.cs), `ruleFilter` query parameter                                                                                                                                                                                                                                                                                    |
| 6   | Connection logs in UI and backend                        | [`useEventStream.ts`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/blob/main/tools/event-viewer/src/useEventStream.ts), worker `SseEndpoints` logging                                                                                                                                                                                                                                                                                            |

Observability (Grafana dashboards, Prometheus metrics) is out of scope for the Milestone 3 demo and is covered separately in [`reports/milestone-2/proof-of-achievement.md`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/blob/main/reports/milestone-2/proof-of-achievement.md).

---

## How to Run

```bash
git clone https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents.git
cd OgmiosDotnet.BlockchainEvents
docker compose up --build          # 15 services — full event pipeline + 4 UI instances
dotnet test                        # 76 tests — 100% pass rate
```

| Service                   | URL                                     |
| ------------------------- | --------------------------------------- |
| Event Viewer (All)        | `http://localhost:4020`                 |
| Event Viewer (Metadata)   | `http://localhost:4021`                 |
| Event Viewer (Governance) | `http://localhost:4022`                 |
| Event Viewer (Address)    | `http://localhost:4023`                 |
| Worker health             | `http://localhost:4000/health`          |
| SSE stream                | `http://localhost:4000/events/stream`   |
| Swagger UI                | `http://localhost:4000/swagger`         |
| Grafana                   | `http://localhost:4002`                 |

### Build UI Locally

```bash
cd tools/event-viewer
npm install
npm run build
VITE_SSE_URL=http://localhost:4000/events/stream npm run dev
```

---

## Demo Video

> **Video link:** [https://youtu.be/uPpgb73ENp8](https://youtu.be/uPpgb73ENp8)

Dedicated Milestone 3 narrated walkthrough — Interactive Consumer & Visualisation. The demo shows filtered Cardano transactions published to the message queue and consumed via HTTP and gRPC, with real-time visualisation on the React dashboard as CloudEvents arrive. The entire system is started with a single `docker compose up --build`; within seconds the worker syncs from the seeded checkpoint and live payloads populate the UI with transaction ID, slot, block height, matched rule, and full transaction data.

The recording demonstrates instant, performant end-to-end event consumption, event detail inspection in the UI, and sample events for each of the four active plug-and-play rules: **Address Match** (Minswap V2 DEX), **Metadata Match**, **All Transactions**, and **Governance & Treasury** (active but infrequent on mainnet). Grafana observability is intentionally omitted — already covered in Milestone 2.

The proof of achievement is available in [`reports/milestone-3/`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/tree/main/reports/milestone-3). Everything is open source and fully customisable — run the demo yourself using the setup instructions above.
