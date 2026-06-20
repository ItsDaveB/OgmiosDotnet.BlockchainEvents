# Blockchain Event Viewer

React-based interactive consumer for the OgmiosDotnet.BlockchainEvents delivery layer. Subscribes via Server-Sent Events (SSE) and visualises filtered Cardano transactions in real time.

## Features

- Real-time SSE consumption with auto-reconnect
- Selectable rule filter configurations (metadata, governance, address match)
- Sortable/filterable event table with metadata, timestamp, and rule type columns
- Connection log panel showing delivery layer connection status
- Event detail drawer with full CloudEvent payload
- Multi-instance support for independent deployment

## Quick Start

```bash
# With full stack (recommended)
docker compose up --build
# Open http://localhost:4020

# Local development
npm install
VITE_SSE_URL=http://localhost:4000/events/stream npm run dev
# Open http://localhost:5173
```

## Build

```bash
npm install
npm run build    # outputs to dist/
npm run preview  # preview production build
```

## Configuration

| Env var | Description |
| ------- | ----------- |
| `VITE_SSE_URL` | Backend SSE endpoint (default: `http://localhost:4000/events/stream`) |
| `VITE_RULE_FILTER` | Pre-selected rule filter ID (e.g. `metadata-key-value`) |
| `VITE_INSTANCE_ID` | Instance label shown in header |

In Docker, use `SSE_URL`, `RULE_FILTER`, and `INSTANCE_ID`. These are injected at runtime via `docker-entrypoint.sh`.

## Project Structure

```
tools/event-viewer/
├── src/
│   ├── App.tsx              # Main dashboard
│   ├── useEventStream.ts    # SSE consumer hook with reconnect
│   ├── ruleConfigs.ts       # Demo rule filter configurations
│   ├── EventDetailDrawer.tsx
│   ├── types.ts
│   └── App.css
├── Dockerfile               # Multi-stage build (npm build + nginx)
├── docker-entrypoint.sh     # Runtime config injection
└── package.json
```

## Documentation

See [docs/ui-consumer-guide.md](../../docs/ui-consumer-guide.md) for architecture overview, API reference, and extension guide.
