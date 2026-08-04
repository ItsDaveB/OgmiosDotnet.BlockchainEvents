# Milestone 4 — Test Report

**Project:** OgmiosDotnet.BlockchainEvents  
**Repository:** [github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents)  
**Milestone:** 4 — Finalisation, Documentation & Community Release  
**Date:** July 2026 (re-verified 4 August 2026)  
**Release:** [`v1.0.0`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/releases/tag/v1.0.0) (`badd71e`)  
**Command:** `dotnet test --configuration Release`

---

## Summary

| Metric | Value |
| ------ | ----- |
| Result | **Successful** |
| Total tests | **80** |
| Passed | **80** |
| Failed | **0** |
| Skipped | **0** |
| Pass rate | **100%** |
| Duration | ~0.1 seconds |

```
Test Run Successful.
Total tests: 80
     Passed: 80
 Total time: 0.135 Seconds
```

---

## Coverage by Area

| Area | Test class(es) | Focus |
| ---- | -------------- | ----- |
| Address filtering | `AddressMatchRuleTests` | Exact address + prefix match, disabled state, evaluate criteria |
| Policy / asset | `PolicyIdAssetRuleTests` | Policy ID and asset name matching |
| Metadata | `MetadataKeyValueRuleTests` | Labels, key/value patterns, MatchAny mode |
| Governance / treasury | `GovernanceTreasuryRuleTests`, `GovernanceExtractorTests` | Actions, treasury, votes, delegation, registration; Ogmios `treasuryWithdrawals` proposal detection |
| Catch-all | `AllTransactionsRuleTests` | Always-match behaviour and defaults |
| Rule engine | `RuleEngineTests` | Multi-rule evaluation, disabled rules, empty set |
| CloudEvents | `BlockchainEventFactoryTests` | Schema, Cardano extensions, unique IDs |
| Checkpoints | `SyncCheckpointTests`, `DaprCheckpointServiceTests` | Serialization, ETag concurrency |
| Broadcast / gRPC | `EventBroadcasterTests`, `BlockchainEventGrpcServiceTests` | Channel capacity, filtering, `GetRecent` buffer, field mapping |

---

## Continuous Integration

| Workflow | Gate |
| -------- | ---- |
| [`ci.yml`](../../.github/workflows/ci.yml) | Build → Test (TRX artifact) → Format → Docker (worker + UI) |
| [`codeql.yml`](../../.github/workflows/codeql.yml) | Security-extended + security-and-quality |
| [`release.yml`](../../.github/workflows/release.yml) | Tag `v*`: test → pack NuGet → publish GHCR images → GitHub Release — [`v1.0.0` run](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/actions/runs/30199956555) successful |

CI uploads TRX results as the `test-results` artifact on every push/PR to `main` and on release builds.

---

## How to Reproduce

```bash
git clone https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents.git
cd OgmiosDotnet.BlockchainEvents
dotnet test --configuration Release
```

Optional TRX output (matches CI):

```bash
dotnet test --configuration Release --logger trx --results-directory TestResults
```

---

## Conclusion

All **80** unit tests pass with a **100%** pass rate. The suite covers built-in rules (including governance, treasury, and metadata scenarios used by the example configurations), Ogmios treasury-withdrawal proposal extraction, CloudEvents emission, checkpoint persistence, and gRPC/SSE delivery primitives required for enterprise adoption.
