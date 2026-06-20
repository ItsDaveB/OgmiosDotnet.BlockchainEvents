#!/bin/sh
# Inject runtime configuration into the built index.html
# This allows each container instance to have its own SSE_URL and INSTANCE_ID
sed -i "s|</head>|<script>window.__SSE_URL__='${SSE_URL:-http://blockchain-events:4000/events/stream}';window.__INSTANCE_ID__='${INSTANCE_ID:-viewer}';window.__RULE_FILTER__='${RULE_FILTER:-}';</script></head>|" /usr/share/nginx/html/index.html

exec "$@"
