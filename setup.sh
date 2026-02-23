#!/usr/bin/env bash
set -euo pipefail

HOSTNAME="blockchain.local"
HOSTS_FILE="/etc/hosts"

echo "🔧 OgmiosDotnet.BlockchainEvents — Setup (optional)"
echo "Adds blockchain.local as an alias for localhost."
echo "All services also work on localhost — this is purely for convenience."
echo ""

# 1. Check /etc/hosts for blockchain.local
if grep -q "$HOSTNAME" "$HOSTS_FILE" 2>/dev/null; then
    echo "✅ $HOSTNAME already in $HOSTS_FILE"
else
    echo "Adding $HOSTNAME to $HOSTS_FILE (requires sudo)..."
    echo "127.0.0.1 $HOSTNAME" | sudo tee -a "$HOSTS_FILE" > /dev/null
    echo "✅ $HOSTNAME added"
fi

# 2. Verify resolution
if ping -c 1 -W 1 "$HOSTNAME" > /dev/null 2>&1; then
    echo "✅ $HOSTNAME resolves correctly"
else
    echo "⚠️  $HOSTNAME does not resolve — you may need to flush DNS cache"
    echo "   macOS:  sudo dscacheutil -flushcache && sudo killall -HUP mDNSResponder"
    echo "   Linux:  sudo systemd-resolve --flush-caches"
fi

echo ""
echo "Services will be available at:"
echo "  http://$HOSTNAME:4000  — Worker (health check at /health)"
echo "  http://$HOSTNAME:4001  — Demo Subscriber"
echo "  http://$HOSTNAME:4002  — Grafana"
echo "  http://$HOSTNAME:4003  — Prometheus"
echo "  http://$HOSTNAME:4004  — Zipkin"
echo "  http://$HOSTNAME:4005  — Dapr Dashboard"
echo "  http://$HOSTNAME:4006  — Redis Commander"
echo ""
echo "Run 'docker compose up --build' to start."
