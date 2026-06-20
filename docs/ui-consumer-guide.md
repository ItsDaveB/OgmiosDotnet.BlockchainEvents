# UI Consumer Guide

This guide explains how to run, extend, and deploy the **Blockchain Event Viewer** — a React-based consumer that subscribes to the event delivery layer and visualises filtered Cardano transactions in real time.

## Architecture Overview

The project separates backend and frontend into independently deployable layers:

```
┌─────────────────────────────────────────────────────────────────┐
│  Backend (src/)                                                 │
│  BlockchainEvents.Worker — rule engine, HTTP/gRPC/SSE delivery  │
│  Port 4000 (HTTP/SSE) · Port 4010 (gRPC)                      │
└────────────────────────────┬────────────────────────────────────┘
                             │  GET /events/stream (SSE)
                             │  Subscribe() gRPC (optional)
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│  Frontend (tools/event-viewer/)                                 │
│  React 19 + Vite — real-time dashboard, rule filter selector    │
│  Port 4020–4023 (Docker) · Port 5173 (local dev)                │
└─────────────────────────────────────────────────────────────────┘
```

The frontend communicates with the backend **only through exposed HTTP APIs** — no shared libraries, no direct database access, no embedded .NET runtime. This enables independent deployment: the UI can be hosted on a CDN while the worker runs in a separate cluster.

### Data Flow

1. The worker connects to Ogmios and evaluates configured transaction rules.
2. Matched transactions are emitted as CloudEvents via Dapr pub/sub, gRPC, and SSE.
3. The React UI opens an `EventSource` connection to `GET /events/stream`.
4. Events arrive as Server-Sent Events (JSON payloads) and populate the dashboard table.
5. Optional `ruleFilter` query parameter restricts the stream to a single rule type.

### Modular Project Structure

| Path | Role | Deployable independently |
| ---- | ---- | ------------------------ |
| `src/` (backend) | .NET Worker — filtering, emission, APIs | Yes — Docker image `blockchain-events` |
| `tools/event-viewer/` (UI) | React dashboard — SSE consumer | Yes — nginx static image |
| `docker-compose.yml` | Orchestrates both layers + infrastructure | Local development |

For milestone evidence, the README maps these to `/backend` and `/ui` aliases:

```
/backend  →  src/                  (.NET event delivery layer)
/ui       →  tools/event-viewer/  (React interactive consumer)
```

## Quick Start

### Docker Compose (recommended)

```bash
git clone https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents.git
cd OgmiosDotnet.BlockchainEvents
docker compose up --build
```

Open the event viewers:

| Instance | URL | Default filter |
| -------- | --- | -------------- |
| All Rules | http://localhost:4020 | All enabled rules |
| Metadata | http://localhost:4021 | `metadata-key-value` |
| Governance | http://localhost:4022 | `governance-treasury` |
| Address Match | http://localhost:4023 | `address-match` |

The **Connection Log** panel at the top of each viewer shows SSE connection status. Worker logs also record subscriber connections:

```
info: SseEndpoints[0] SSE subscriber connected from ::ffff:172.18.0.1 (filter: metadata-key-value)
```

### Local Development

**Terminal 1 — Backend:**

```bash
docker compose up redis placement zipkin blockchain-events
```

**Terminal 2 — UI:**

```bash
cd tools/event-viewer
npm install
VITE_SSE_URL=http://localhost:4000/events/stream npm run dev
```

Open http://localhost:5173. Use the rule filter buttons in the header to switch between filter configurations.

## API Reference (Consumer Endpoints)

### SSE Stream

```
GET /events/stream
GET /events/stream?ruleFilter=metadata-key-value
```

| Parameter | Type | Description |
| --------- | ---- | ----------- |
| `ruleFilter` | string (optional) | Filter by rule ID. Omit for all events. |

**Available rule IDs:** `address-match`, `metadata-key-value`, `governance-treasury`, `policy-id-asset`, `all-transactions`

Each SSE message contains a JSON CloudEvent payload:

```json
{
  "id": "tx-abc123-metadata-key-value-1705312200000",
  "type": "io.cardano.transaction.metadata-key-value",
  "time": "2024-01-15T10:30:00.000Z",
  "cardanoSlot": 115545883,
  "cardanoNetwork": "mainnet",
  "data": {
    "transactionId": "abc123...",
    "ruleId": "metadata-key-value",
    "ruleName": "Metadata Key Value",
    "matchedCriteria": { "metadata_labels": [674] },
    "transaction": { "fee": 200000, "outputAddresses": ["addr1..."] }
  }
}
```

### gRPC Stream (alternative consumer)

```bash
grpcurl -plaintext -import-path . -proto protos/blockchain_events.proto \
  -d '{"rule_filter": "governance-treasury"}' \
  localhost:4010 blockchain_events.BlockchainEventService/Subscribe
```

## Extending the UI

### Adding a Custom Rule Filter

Edit `tools/event-viewer/src/ruleConfigs.ts`:

```typescript
export const DEMO_RULE_CONFIGS: RuleFilterConfig[] = [
  // ... existing configs ...
  {
    id: 'policy',
    label: 'Policy ID Asset',
    description: 'Token mints and asset transfers by policy ID',
    ruleFilter: 'policy-id-asset',
    chipClass: 'default',
  },
];
```

The UI reconnects automatically when the filter changes, passing `?ruleFilter=` to the SSE endpoint.

### Building a Custom Consumer Hook

The `useEventStream` hook demonstrates the core consumer pattern:

```typescript
import { useEffect, useState } from 'react';

export function useCustomConsumer(baseUrl: string, ruleFilter: string | null) {
  const [events, setEvents] = useState([]);
  const [status, setStatus] = useState('connecting');

  useEffect(() => {
    const url = ruleFilter
      ? `${baseUrl}?ruleFilter=${encodeURIComponent(ruleFilter)}`
      : baseUrl;

    const source = new EventSource(url);
    source.onopen = () => setStatus('connected');
    source.onmessage = (e) => {
      const event = JSON.parse(e.data);
      setEvents(prev => [event, ...prev].slice(0, 500));
    };
    source.onerror = () => setStatus('disconnected');

    return () => source.close();
  }, [baseUrl, ruleFilter]);

  return { events, status };
}
```

### Adding a Table Column

Extend the column definitions in `App.tsx`:

```typescript
{
  id: 'era',
  header: 'Era',
  accessorFn: (row) => row.cardanoEra,
  cell: ({ getValue }) => <span>{getValue() as string}</span>,
  size: 90,
},
```

### Environment Variables

| Variable | Default | Description |
| -------- | ------- | ----------- |
| `SSE_URL` / `VITE_SSE_URL` | `http://localhost:4000/events/stream` | Backend SSE endpoint |
| `RULE_FILTER` / `VITE_RULE_FILTER` | (empty) | Pre-selected rule filter |
| `INSTANCE_ID` / `VITE_INSTANCE_ID` | random | Display label for multi-instance deployments |

In Docker, these are injected at container startup via `docker-entrypoint.sh`.

## Deployment

### UI Only (CDN / Static Hosting)

```bash
cd tools/event-viewer
npm run build
# Deploy dist/ to any static host (S3, Netlify, nginx, etc.)
```

Set `SSE_URL` to point at your deployed backend's SSE endpoint. CORS is enabled on the worker for browser-based consumers.

### Backend Only

```bash
docker build -f src/BlockchainEvents.Worker/Dockerfile -t blockchain-events .
docker run -p 4000:4000 -p 4010:4010 blockchain-events
```

Any consumer (React, Python, Node.js) can subscribe via HTTP SSE or gRPC without the UI.

## Demo Scenarios

The dashboard supports three filtering scenarios required for milestone demonstration:

| Scenario | Rule ID | What it shows |
| -------- | ------- | ------------- |
| **Metadata-based** | `metadata-key-value` | Transactions with on-chain metadata labels and key/value patterns |
| **Governance/Treasury** | `governance-treasury` | CIP-1694 governance votes and treasury-related actions |
| **Address Match** | `address-match` | DEX order addresses (Minswap V2) and wallet prefix matches |

Switch between filters using the header buttons. Each filter produces visually distinct results — different rule chips, metadata columns, and event counts in the stats bar.

## Related Documentation

- [Architecture Overview](architecture.md) — full system design
- [Event Schema](event-schema.md) — CloudEvents payload specification
- [Integration Guide](integration-guide.md) — building custom backend rules
- [OpenAPI Spec](openapi.json) — HTTP API reference
