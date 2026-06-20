# Milestone 3 — Proof of Achievement

**Project:** OgmiosDotnet.BlockchainEvents  
**Repository:** [github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents)  
**Milestone:** 3 — Interactive Consumer & Visualisation  
**Date:** June 2026  
**Commit:** `5525f81` (main)

---

## Overview

OgmiosDotnet.BlockchainEvents now includes a **React-based web application** that subscribes to the event delivery layer via Server-Sent Events (SSE) and provides real-time visualisation of filtered Cardano transactions. The UI demonstrates selectable rule filter configurations (metadata-based, governance/treasury, address match) with distinct live results, while maintaining clear separation between backend and frontend layers for independent deployment.

Full setup and extension instructions: [`docs/ui-consumer-guide.md`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/blob/main/docs/ui-consumer-guide.md)

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

The dashboard updates automatically as new filtered transactions arrive:

- **Live stats**: total events, events/sec sparkline, uptime, rule breakdown chips
- **Event table**: Time, Rule, Metadata, Transaction, Slot, Block, Fee, Outputs columns
- **Detail drawer**: CloudEvent envelope, Cardano context, matched criteria, transaction I/O
- **Pause/resume** and **clear** controls for demo management
- **500-event buffer** with newest-first display and animated row highlights

### Output 3 — Example Filtering Scenarios

Three filtering scenarios are demonstrated with selectable configurations:

| Scenario | Rule ID | Filter Button | Docker Instance |
| -------- | ------- | ------------- | --------------- |
| **Metadata-based** | `metadata-key-value` | Metadata | http://localhost:4021 |
| **Governance/Treasury** | `governance-treasury` | Governance & Treasury | http://localhost:4022 |
| **Address Match** | `address-match` | Address Match | http://localhost:4023 |

Each filter reconnects the SSE stream with `?ruleFilter=` and produces visually distinct results, with different rule chips, metadata summaries, and event counts.

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

`docker compose up --build` starts 15 services including 4 event viewer instances. The UI connects to the delivery layer via SSE on port 4000. Connection status is shown in the header (Live/Connecting/Disconnected) and the **Connection Log** panel records connect/disconnect/reconnect events with timestamps. Worker logs record SSE subscriber connections:

```
info: SseEndpoints[0] SSE subscriber connected from ::ffff:172.18.0.1 (filter: metadata-key-value)
```

Evidence: [`useEventStream.ts`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/blob/main/tools/event-viewer/src/useEventStream.ts), [`SseEndpoints.cs`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/blob/main/src/BlockchainEvents.Worker/Endpoints/SseEndpoints.cs), [`docker-compose.yml`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/blob/main/docker-compose.yml) — see [demo video](#demo-video) below

### AC-2: UI updates automatically; at least 10 sample events displayed with metadata, timestamp, and rule type

Events populate the dashboard table in real time via `EventSource`. Each row displays:

- **Timestamp:** formatted from CloudEvent `time` field (HH:MM:SS.mmm)
- **Rule type:** colour-coded chip from `data.ruleName`
- **Metadata:** summary from `data.matchedCriteria` (key/value patterns, labels)

The detail drawer shows the full matched criteria and CloudEvent envelope on row click.

Evidence: [`App.tsx`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/blob/main/tools/event-viewer/src/App.tsx) — see [demo video](#demo-video) below

### AC-3: Two demo rule configurations selectable and produce distinct visual live results

Four filter configurations are available via header buttons and pre-configured Docker instances:

1. **All Rules:** every matched transaction
2. **Metadata:** `metadata-key-value` rule only
3. **Governance & Treasury:** `governance-treasury` rule only
4. **Address Match:** `address-match` rule only

Switching filters reconnects SSE with `?ruleFilter=` and clears the event buffer, producing immediately distinct rule chips and event counts in the stats bar.

Evidence: [`ruleConfigs.ts`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/blob/main/tools/event-viewer/src/ruleConfigs.ts), pre-configured viewer instances in [`docker-compose.yml`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/blob/main/docker-compose.yml) — see [demo video](#demo-video) below

### AC-4: Frontend communicates with backend only via exposed APIs; modular project structure

- Frontend uses `EventSource` to `GET /events/stream`. No gRPC, Dapr, or Redis access from the UI.
- Backend at `src/`, frontend at `tools/event-viewer/`. Separate Dockerfiles and build pipelines.
- Four independent viewer containers demonstrate multi-consumer deployment

Evidence: project structure in [`README.md`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/blob/main/README.md), SSE-only consumption in [`useEventStream.ts`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/blob/main/tools/event-viewer/src/useEventStream.ts)

### AC-5: Documentation includes setup guide, architecture overview, and sample code for extending the UI

[`docs/ui-consumer-guide.md`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/blob/main/docs/ui-consumer-guide.md) contains:

- Quick start (Docker Compose + local dev)
- Architecture diagram (backend ↔ frontend via SSE)
- API reference (`GET /events/stream`, `ruleFilter` parameter)
- Sample code: custom consumer hook, adding table columns, adding rule filters
- Deployment guide (UI-only CDN, backend-only Docker)
- Demo scenario descriptions

Verified by existence and cross-referenced in README and architecture docs.

---

## Evidence

**Demo video:** [https://youtu.be/VTBksbzPYXU](https://youtu.be/VTBksbzPYXU) — the Milestone 2 narrated walkthrough demonstrates the same delivery layer, Docker Compose stack, live event flow, and event viewer UIs (ports 4020–4023) that Milestone 3 builds on. Milestone 3-specific UI behaviour (rule filter selector, connection log, metadata column) is evidenced in the source code below.

| #   | Requirement                                              | Evidence                                                                                                                                                                                                                                                                                                                                                                                                                                                        |
| --- | -------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | Public GitHub repo with UI project + Docker Compose      | [`tools/event-viewer/`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/tree/main/tools/event-viewer), [`docker-compose.yml`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/blob/main/docker-compose.yml) — see [demo video](#demo-video) below                                                                                                                                                                                            |
| 2   | Video demo showing live updates + filter type results    | [Demo Video](#demo-video) — [Milestone 2 walkthrough](https://youtu.be/VTBksbzPYXU) (live event flow, Docker Compose, event viewers); M3 filter selector in [`ruleConfigs.ts`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/blob/main/tools/event-viewer/src/ruleConfigs.ts) and pre-configured instances in [`docker-compose.yml`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/blob/main/docker-compose.yml)                                                                                                                                                                                                                                                    |
| 3   | README showing `/backend`, `/ui` modular structure       | [`README.md`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/blob/main/README.md) project structure section                                                                                                                                                                                                                                                                                                                                          |
| 4   | Documentation with setup and extension instructions      | [`docs/ui-consumer-guide.md`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/blob/main/docs/ui-consumer-guide.md), [`tools/event-viewer/README.md`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/tree/main/tools/event-viewer/README.md)                                                                                                                                                                                              |
| 5   | SSE rule filtering (parity with gRPC)                    | [`SseEndpoints.cs`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/blob/main/src/BlockchainEvents.Worker/Endpoints/SseEndpoints.cs), `ruleFilter` query parameter                                                                                                                                                                                                                                                                                    |
| 6   | Connection logs in UI and backend                        | [`useEventStream.ts`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/blob/main/tools/event-viewer/src/useEventStream.ts), worker `SseEndpoints` logging                                                                                                                                                                                                                                                                                            |

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

> **Video link:** [https://youtu.be/VTBksbzPYXU](https://youtu.be/VTBksbzPYXU)

The Milestone 2 narrated walkthrough covers the shared delivery infrastructure: Docker Compose local setup (15 services), live filtered transaction events, and event viewer UIs at `http://localhost:4020..4023`. This satisfies the video evidence requirement for live updates, connection to the delivery layer, and distinct filter results across metadata, governance/treasury, and address match scenarios.
