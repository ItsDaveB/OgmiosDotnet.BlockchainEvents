#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
EXAMPLE="${1:-}"

usage() {
  echo "Usage: $0 <governance|treasury|metadata>"
  echo
  echo "Starts docker compose with the selected example appsettings.json mounted into the worker."
  exit 1
}

[[ -n "$EXAMPLE" ]] || usage
EXAMPLE_FILE="$ROOT/examples/$EXAMPLE/appsettings.json"
[[ -f "$EXAMPLE_FILE" ]] || { echo "Unknown example: $EXAMPLE"; usage; }

echo "Using example configuration: examples/$EXAMPLE/appsettings.json"
echo "Starting stack (Ctrl+C to stop)..."
cd "$ROOT"
WORKER_APPSETTINGS="./examples/${EXAMPLE}/appsettings.json" docker compose up --build
