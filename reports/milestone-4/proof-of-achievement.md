# Milestone 4 — Proof of Achievement

**Project:** OgmiosDotnet.BlockchainEvents  
**Repository:** [github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents)  
**Milestone:** 4 — Finalisation, Documentation & Community Release  
**Date:** July 2026  
**Commit:** `badd71e` (main / `v1.0.0`)  
**Release tag:** [`v1.0.0`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/releases/tag/v1.0.0)  
**Release workflow:** [successful run](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/actions/runs/30199956555)

---

## Overview

Milestone 4 packages the complete system for public adoption: versioned Docker images on GHCR, a GitHub Release with configuration templates and setup instructions, NuGet libraries for the Domain and Engine layers (nuget.org + GitHub Packages), example filter profiles (governance, treasury, metadata), enterprise-oriented quality gates, community onboarding documentation, a full test report, and close-out media (report; YouTube video and social post pending).

Prior milestones established filtering ([M1](../milestone-1/proof-of-achievement.md)), delivery ([M2](../milestone-2/proof-of-achievement.md)), and visualisation ([M3](../milestone-3/proof-of-achievement.md)). This milestone makes those components releasable and adoptable.

---

## Milestone Outputs

### Output 1 — Containerised Components on a Public Registry

Release workflow (`.github/workflows/release.yml`) builds and pushes versioned `linux/amd64` images on every `v*` tag (amd64 chosen for reliable Grpc.Tools/protoc publish in CI):

| Image | GHCR path | Published tag |
| ----- | --------- | ------------- |
| Worker | `ghcr.io/itsdaveb/ogmiosdotnet.blockchainevents` | `1.0.0` / `1.0` / `1` / SHA |
| Event viewer | `ghcr.io/itsdaveb/ogmiosdotnet.blockchainevents/event-viewer` | `1.0.0` / `1.0` / `1` / SHA |

| Package page |
| ------------ |
| [Worker](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/pkgs/container/ogmiosdotnet.blockchainevents) |
| [Event viewer](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/pkgs/container/ogmiosdotnet.blockchainevents%2Fevent-viewer) |

Local development continues to use `docker compose up --build`.

### Output 2 — Versioned GitHub Release

[`v1.0.0`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/releases/tag/v1.0.0) is published with release notes, attached `.nupkg` / `.snupkg` artifacts, and links to setup docs and example configurations. Source, templates, and instructions remain in-repo:

| Asset | Location |
| ----- | -------- |
| Setup instructions | [`README.md`](../../README.md), [`docs/getting-started.md`](../../docs/getting-started.md) |
| Configuration templates | [`src/BlockchainEvents.Worker/appsettings.json`](../../src/BlockchainEvents.Worker/appsettings.json), [`examples/`](../../examples/) |
| Dapr / observability | [`dapr/`](../../dapr/), [`observability/`](../../observability/) |

### Output 3 — Example Configurations

| Example | Path | Use case |
| ------- | ---- | -------- |
| Governance | [`examples/governance/`](../../examples/governance/) | CIP-1694 governance actions + votes |
| Treasury | [`examples/treasury/`](../../examples/treasury/) | Treasury withdrawals (+ governance actions) |
| Metadata | [`examples/metadata/`](../../examples/metadata/) | CIP-20 label 674 / key patterns |

```bash
./examples/run-example.sh governance
./examples/run-example.sh treasury
./examples/run-example.sh metadata
```

Implementation detail: rules bind from `Rules:*` configuration sections (technical debt fix vs hardcoded `Program.cs` options). Compose mounts examples via `WORKER_APPSETTINGS`.

### Output 4 — Code Quality & Technical Debt

| Improvement | Detail |
| ----------- | ------ |
| Config-bound rules | `Program.cs` uses `Configure<T>(GetSection(...))` for all built-in rules |
| PolicyIdAsset registration | Rule wired and configurable (disabled by default) |
| Packaging metadata | `Directory.Build.props` + packable Domain/Engine projects |
| CI quality gates | Format verify, CodeQL (`security-extended` + `security-and-quality`), Docker builds, TRX upload |
| Tests | 76 unit tests, 100% pass — see [`test-report.md`](test-report.md) |
| Release pipeline | Tag-driven GHCR + NuGet.org Trusted Publishing + GitHub Packages + GitHub Release |

### Output 5 — Community Onboarding Materials

| Document | Purpose |
| -------- | ------- |
| [`docs/getting-started.md`](../../docs/getting-started.md) | Run, extend, and adopt independently |
| [`examples/README.md`](../../examples/README.md) | End-to-end example verification |
| [`docs/integration-guide.md`](../../docs/integration-guide.md) | Custom rules |
| [`docs/ui-consumer-guide.md`](../../docs/ui-consumer-guide.md) | UI extension |
| [`CONTRIBUTING.md`](../../CONTRIBUTING.md) | Contributor workflow |
| [`README.md`](../../README.md) | Quick start + package/image references |

### Output 6 — NuGet Packages

| Package ID | nuget.org | Contents |
| ---------- | --------- | -------- |
| `OgmiosDotnet.BlockchainEvents.Domain` | [1.0.0](https://www.nuget.org/packages/OgmiosDotnet.BlockchainEvents.Domain/1.0.0) | Models, abstractions, CloudEvents types |
| `OgmiosDotnet.BlockchainEvents.Engine` | [1.0.0](https://www.nuget.org/packages/OgmiosDotnet.BlockchainEvents.Engine/1.0.0) | Rule engine + built-in filters |

Published on release to **nuget.org** via [Trusted Publishing](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing) (GitHub OIDC → short-lived API key) and to **GitHub Packages**. Also attached to the [GitHub Release](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/releases/tag/v1.0.0).

```bash
dotnet add package OgmiosDotnet.BlockchainEvents.Domain --version 1.0.0
dotnet add package OgmiosDotnet.BlockchainEvents.Engine --version 1.0.0
```

### Output 7 — Test Report

[`test-report.md`](test-report.md) — **76/76 tests passing** (`dotnet test --configuration Release`).

### Output 8 — Close-out Report

[`close-out-report.md`](close-out-report.md) — deliverables, successes, learnings, and future improvements.

### Output 9 — Close-out Video (YouTube)

Narrated walkthrough: setup → example filters → live visualisation → monitoring.

> **Video link:** `TBD` — add YouTube URL after upload

### Output 10 — Social Media Demo Post

Public post from [@ItsDave_ADA](https://x.com/ItsDave_ADA) showcasing functioning filters, developer details, and transaction visualisation, with view/engagement metrics.

> **Post link / metrics:** `TBD` — add after publishing

---

## Acceptance Criteria

### AC-1: Publishable components containerised, versioned, and published to a public registry — **Met**

Release workflow publishes worker + event-viewer `linux/amd64` images to GHCR with semver tags `1.0.0`. Evidence: [worker package](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/pkgs/container/ogmiosdotnet.blockchainevents), [event-viewer package](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/pkgs/container/ogmiosdotnet.blockchainevents%2Fevent-viewer), [release run](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/actions/runs/30199956555).

### AC-2: Full release versioned and publicly available on GitHub — **Met**

GitHub Release [`v1.0.0`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/releases/tag/v1.0.0) includes source tag, NuGet artifacts, and links to configuration templates and setup docs.

### AC-3: Example configurations provided and verified end-to-end — **Met**

Governance, treasury, and metadata examples under [`examples/`](../../examples/) with documented verification steps (health, SSE, event viewer, logs).

### AC-4: Enterprise standards for quality, security, maintainability; technical debt resolved — **Met**

Config-bound rules, CI format + CodeQL + tests, packable libraries, 76/76 tests passing. Evidence: [`test-report.md`](test-report.md), [Actions](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/actions).

### AC-5: Onboarding documentation enables independent adoption — **Met**

[`docs/getting-started.md`](../../docs/getting-started.md) covers run / extend / adopt with links to architecture, integration, UI, and examples.

### AC-6: NuGet package exists for published version — **Met**

`OgmiosDotnet.BlockchainEvents.Domain` and `.Engine` **1.0.0** on [nuget.org](https://www.nuget.org/packages/OgmiosDotnet.BlockchainEvents.Domain/1.0.0) (Trusted Publishing) and GitHub Packages.

### AC-7: Test report exists; all tests passing — **Met**

[`test-report.md`](test-report.md) — 76 passed, 0 failed.

### AC-8: Close-out report summarises outcomes, learnings, improvements — **Met**

[`close-out-report.md`](close-out-report.md).

### AC-9: Close-out video summarises work, setup, custom filters, live visualisation — **Pending**

YouTube link in [Demo Video](#demo-video) section (fill after upload).

### AC-10: Social media demo post with metrics — **Pending**

Linked in evidence table (fill after posting from [@ItsDave_ADA](https://x.com/ItsDave_ADA)).

---

## Evidence

| # | Requirement | Evidence | Status |
| - | ----------- | -------- | ------ |
| 1 | Public container registry (versioned images) | [Worker GHCR](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/pkgs/container/ogmiosdotnet.blockchainevents), [Event viewer GHCR](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/pkgs/container/ogmiosdotnet.blockchainevents%2Fevent-viewer) — tags `1.0.0` | Done |
| 2 | Versioned GitHub release | https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/releases/tag/v1.0.0 | Done |
| 3 | Example configurations (gov / treasury / metadata) | https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/tree/main/examples | Done |
| 4 | Source quality + CI | [Repository](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents), [Actions](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/actions/runs/30199956555), [`test-report.md`](test-report.md) | Done |
| 5 | README + onboarding docs | [`README.md`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/blob/main/README.md), [`docs/getting-started.md`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/blob/main/docs/getting-started.md) | Done |
| 6 | NuGet package | [Domain 1.0.0](https://www.nuget.org/packages/OgmiosDotnet.BlockchainEvents.Domain/1.0.0), [Engine 1.0.0](https://www.nuget.org/packages/OgmiosDotnet.BlockchainEvents.Engine/1.0.0) | Done |
| 7 | Test report (all passing) | [`test-report.md`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/blob/main/reports/milestone-4/test-report.md) | Done |
| 8 | Close-out report | [`close-out-report.md`](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/blob/main/reports/milestone-4/close-out-report.md) | Done |
| 9 | Close-out video | `TBD` YouTube URL | Pending |
| 10 | Social media post + metrics | `TBD` — [@ItsDave_ADA](https://x.com/ItsDave_ADA) | Pending |

---

## How to Run

```bash
git clone https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents.git
cd OgmiosDotnet.BlockchainEvents
git checkout v1.0.0
docker compose up --build          # default demo rules
./examples/run-example.sh metadata # or governance | treasury
dotnet test                        # 76 tests — 100% pass rate
```

| Service | URL |
| ------- | --- |
| Event viewers | `http://localhost:4020..4023` |
| Worker / SSE | `http://localhost:4000` |
| Grafana | `http://localhost:4002` |
| Swagger | `http://localhost:4000/swagger` |

Pull published images:

```bash
docker pull ghcr.io/itsdaveb/ogmiosdotnet.blockchainevents:1.0.0
docker pull ghcr.io/itsdaveb/ogmiosdotnet.blockchainevents/event-viewer:1.0.0
```

---

## Demo Video

> **Video link:** `TBD`

Close-out walkthrough covering setup, custom transaction filters (governance / treasury / metadata examples), live visualisation in the React event viewer, and monitoring via Grafana.

---

## Publication Checklist (operator)

1. [x] Merge PR / commit Milestone 4 changes to `main`
2. [x] Update **Commit** field in this file to the release SHA (`badd71e`)
3. [x] nuget.org → Trusted Publishing policy for `ItsDaveB` / `OgmiosDotnet.BlockchainEvents` / `release.yml`
4. [x] Set repo secret `NUGET_USER` to nuget.org **profile name** (policy creator)
5. [x] Tag and push `v1.0.0`
6. [x] Confirm GHCR images, GitHub Release, and NuGet packages (nuget.org + GitHub Packages)
7. [ ] Record & upload YouTube close-out video; paste URL above
8. [ ] Publish social demo post from [@ItsDave_ADA](https://x.com/ItsDave_ADA); paste URL + metrics
9. [ ] Submit Milestone 4 evidence links on the proposal portal
