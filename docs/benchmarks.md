# Benchmark Results — Milestone 2

**Project:** OgmiosDotnet.BlockchainEvents
**Date:** April 2026
**Environment:** Docker Compose (14 services), Apple M-series / 16 GB RAM, macOS
**Network:** Cardano mainnet via hosted Ogmios (Demeter.run)
**Consumers:** 2 — Demo Subscriber (Redis Streams pub/sub) + SSE Event Viewer (HTTP streaming)

---

## Test Configuration

| Parameter              | Value                                                                                |
| ---------------------- | ------------------------------------------------------------------------------------ |
| Rules enabled          | 4 — AddressMatch (Minswap V2), MetadataKeyValue, GovernanceTreasury, AllTransactions |
| Active processing time | ~23 minutes continuous catch-up from historical checkpoint                           |
| Slot range processed   | 155,268,704 → 155,909,868 (641,164 slots)                                            |
| Block height range     | 11,844,035 → 11,875,626 (31,591 blocks)                                              |
| Consumers              | 2 — demo-subscriber (pub/sub via Redis Streams), event-viewer-1 (SSE)                |

---

## Prometheus Metrics Used

| Metric                                            | Type      | Description                                 |
| ------------------------------------------------- | --------- | ------------------------------------------- |
| `blockchain_blocks_processed_total`               | Counter   | Total blocks processed                      |
| `blockchain_transactions_processed_total`         | Counter   | Total transactions evaluated by rule engine |
| `blockchain_events_emitted_total{rule}`           | Counter   | Events emitted, labelled by rule            |
| `blockchain_events_failed_total{rule,error_type}` | Counter   | Failed event emissions                      |
| `blockchain_events_processing_latency_seconds`    | Histogram | Per-event processing latency (p50/p90/p99)  |
| `blockchain_events_in_flight`                     | Gauge     | Events currently being published            |
| `blockchain_enabled_rules_total`                  | Gauge     | Number of enabled rules                     |
| `blockchain_last_processed_slot`                  | Gauge     | Slot number of the last processed block     |

---

## Scenario 1: Sustained Catch-Up (Historical Block Processing)

**Description:** Worker catching up from a historical checkpoint, processing 31,591 mainnet blocks continuously with 2 consumers attached.

### Throughput

| Metric                       | Measured Value                                                |
| ---------------------------- | ------------------------------------------------------------- |
| Blocks processed             | 31,591 (23.1 blocks/sec)                                      |
| Transactions processed       | 283,796 (207.5 tx/sec)                                        |
| Total events emitted         | 388,035 (283.7 events/sec)                                    |
| — All Transactions           | 283,796 (207.5/sec)                                           |
| — Address Match (Minswap V2) | 19,601 (14.3/sec)                                             |
| — Metadata Key/Value Match   | 84,370 (61.7/sec)                                             |
| — Governance & Treasury      | 268 (0.2/sec)                                                 |
| Rule match rate              | 36.7% of transactions matched ≥1 rule beyond all-transactions |
| Error rate                   | 0%                                                            |
| Events failed                | 0                                                             |

### Latency (from Prometheus histogram, n = 283,796 events)

| Percentile | All Transactions | Address Match | Metadata K/V |
| ---------- | ---------------- | ------------- | ------------ |
| Mean       | 0.073 ms         | 0.078 ms      | 0.083 ms     |
| p50        | < 1 ms           | < 1 ms        | < 1 ms       |
| p90        | < 1 ms           | < 1 ms        | < 1 ms       |
| p99        | 1–5 ms           | < 1 ms        | < 1 ms       |

**Latency distribution (All Transactions rule):**

| Bucket  | Cumulative % | Count   |
| ------- | ------------ | ------- |
| ≤ 1 ms  | 98.94%       | 280,784 |
| ≤ 5 ms  | 99.94%       | 283,620 |
| ≤ 10 ms | 99.98%       | 283,735 |
| ≤ 25 ms | 100.00%      | 283,787 |
| > 25 ms | 0.00%        | 0       |

### Memory & CPU

| Container                 | Memory Usage | CPU                  |
| ------------------------- | ------------ | -------------------- |
| Worker                    | 106–184 MiB  | High during catch-up |
| Worker sidecar            | 48–79 MiB    | < 1%                 |
| Demo Subscriber           | 38–40 MiB    | < 1%                 |
| Subscriber sidecar        | 38–44 MiB    | < 1%                 |
| Redis (with 4.7M entries) | 10.9 GiB     | Variable             |

### Consumer Delivery

| Metric                           | Value     |
| -------------------------------- | --------- |
| Demo Subscriber events received  | 206,547+  |
| Redis Stream entries accumulated | 4,735,984 |
| Events lost                      | 0         |

---

## Scenario 2: Consumer Failure & Redelivery

**Description:** Stop the demo-subscriber container while events are flowing, observe queue accumulation, then restart and verify redelivery.

**Procedure:**

1. Full stack running, events flowing to both consumers
2. `docker compose stop demo-subscriber demo-subscriber-dapr`
3. Monitor Redis Stream depth at 5s, 10s, 15s intervals
4. `docker compose start demo-subscriber && sleep 3 && docker compose start demo-subscriber-dapr`
5. Observe subscriber event counter climbing from 0 (counter resets on restart)

| Metric                           | Measured Value                 |
| -------------------------------- | ------------------------------ |
| Queue growth during 15s downtime | 2,000+ messages accumulated    |
| Time to first redelivery         | < 3 s after subscriber restart |
| Redelivery success rate          | 100%                           |
| Events lost                      | 0                              |
| Dead-letter queue events         | 0                              |

---

## Scenario 3: Worker Restart & Checkpoint Recovery

**Description:** Stop the worker mid-sync, wait, restart, and verify it resumes from the saved checkpoint.

**Procedure:**

1. Worker running, advancing blocks at slot S
2. `docker compose stop blockchain-events blockchain-events-dapr`
3. Wait 10 seconds
4. `docker compose start blockchain-events && sleep 5 && docker compose start blockchain-events-dapr`
5. Observe worker logs for checkpoint resume

| Metric                                            | Measured Value                       |
| ------------------------------------------------- | ------------------------------------ |
| Checkpoint save interval                          | Every 100 blocks + on event emission |
| Maximum block re-processing on restart            | < 100 blocks                         |
| Resume time (from container start to first block) | < 5 s                                |
| Data loss                                         | 0                                    |

---

## Scenario 4: Resiliency Configuration

**Description:** The message broker resiliency is configured via policy files. These settings govern retry behaviour when Redis or consumers are temporarily unavailable.

**Configuration (from `resiliency.yaml`):**

```yaml
retries:
  pubsubRetry:
    policy: constant
    duration: 2s
    maxRetries: 10
  criticalRetry:
    policy: exponential
    maxInterval: 30s
    maxRetries: 20
circuitBreakers:
  pubsubCircuitBreaker:
    maxRequests: 1
    interval: 8s
    timeout: 45s
    trip: consecutiveFailures > 8
```

| Parameter                     | Value                                                 |
| ----------------------------- | ----------------------------------------------------- |
| Outbound retry policy         | Constant 2s interval, 10 max retries                  |
| Inbound retry policy          | Exponential backoff, 20 max retries, 30s max interval |
| Circuit breaker trips after   | > 8 consecutive failures                              |
| Circuit breaker reset timeout | 45 s                                                  |

---

## Scenario 5: gRPC Streaming

**Description:** gRPC subscriber connected via `grpcurl` on port 4010, receiving live events.

| Metric                              | Value                                              |
| ----------------------------------- | -------------------------------------------------- |
| gRPC stream throughput              | Matches event emission rate (up to 284 events/sec) |
| Channel capacity                    | 1,000 events (bounded, DropOldest)                 |
| Filtered subscription (single rule) | Reduces throughput to rule-matched events only     |

---

## Prometheus Queries

```promql
# Block processing rate (per second, 1-minute window)
rate(blockchain_blocks_processed_total[1m])

# Transaction throughput
rate(blockchain_transactions_processed_total[1m])

# Event emission rate by rule
rate(blockchain_events_emitted_total[1m])

# Processing latency percentiles
histogram_quantile(0.50, rate(blockchain_events_processing_latency_seconds_bucket[5m]))
histogram_quantile(0.90, rate(blockchain_events_processing_latency_seconds_bucket[5m]))
histogram_quantile(0.99, rate(blockchain_events_processing_latency_seconds_bucket[5m]))

# Memory stability
process_working_set_bytes{job="blockchain-events"}
```

---

## Summary

| Scenario           | Key Metric             | Measured Value       | Error Rate | Data Loss |
| ------------------ | ---------------------- | -------------------- | ---------- | --------- |
| Sustained catch-up | Block throughput       | 23.1 blocks/sec      | 0%         | 0         |
| Sustained catch-up | Transaction throughput | 207.5 tx/sec         | 0%         | 0         |
| Sustained catch-up | Event emission rate    | 283.7 events/sec     | 0%         | 0         |
| Sustained catch-up | Processing latency p99 | 1–5 ms               | 0%         | 0         |
| Consumer failure   | Redelivery             | 100%, < 3 s          | 0%         | 0         |
| Worker restart     | Checkpoint resume      | < 5 s, < 100 blocks  | 0%         | 0         |
| gRPC streaming     | Stream throughput      | Up to 284 events/sec | 0%         | 0         |

All measurements taken from live Cardano mainnet processing via Prometheus counters and histograms. The pipeline demonstrates at-least-once delivery with sub-millisecond median latency and zero data loss across all tested scenarios.
