# Contributing to OgmiosDotnet.BlockchainEvents

Thank you for your interest in contributing! This document covers setup, workflow conventions, and how we review changes.

## Code of Conduct

Be respectful and constructive in issues, pull requests, and discussions. Assume good intent, focus on the technical merits of a change, and keep feedback actionable.

## Development Setup

### Prerequisites

- .NET 10 SDK
- Docker & Docker Compose
- Git
- Node.js 20+ (only if working on `tools/event-viewer`)

### Getting Started

```bash
# Clone the repository
git clone https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents.git
cd OgmiosDotnet.BlockchainEvents

# Optional: adds blockchain.local hostname (use instead of localhost)
./setup.sh

# Restore dependencies
dotnet restore

# Build the solution
dotnet build

# Run tests
dotnet test
```

UI (optional):

```bash
cd tools/event-viewer
npm install
npm run dev
```

## Project Structure

```
src/
├── BlockchainEvents.Domain/   # Core models, abstractions, events
├── BlockchainEvents.Engine/   # Rule engine and built-in rules
└── BlockchainEvents.Worker/   # .NET Worker service
tests/
└── BlockchainEvents.Tests/    # Unit tests
docs/
├── getting-started.md         # Community onboarding
├── architecture.md            # System architecture
├── event-schema.md            # Event schema specification
└── integration-guide.md       # Custom rule development guide
examples/
├── governance/                # Example: governance actions + votes
├── treasury/                  # Example: treasury withdrawals
└── metadata/                  # Example: metadata label filtering
tools/
└── event-viewer/              # React SSE dashboard
```

## How to Contribute

1. **Search existing issues** before opening a new one ([Issues](https://github.com/ItsDaveB/OgmiosDotnet.BlockchainEvents/issues)).
2. **Open an issue** for bugs or feature proposals when the change is non-trivial — discuss approach first for larger work.
3. **Fork** the repository and create a branch from `main`.
4. **Implement** with tests and docs updates as needed.
5. **Open a pull request** using the PR checklist below.
6. Address review feedback; maintainers will merge when CI is green and the checklist is satisfied.

Small docs/typo fixes can go straight to a PR without a prior issue.

## Branch Naming

Use a short, descriptive prefix:

| Prefix                 | Use for                         |
| ---------------------- | ------------------------------- |
| `feature/<short-name>` | New behaviour or capability     |
| `fix/<short-name>`     | Bug fix                         |
| `docs/<short-name>`    | Documentation only              |
| `chore/<short-name>`   | Tooling, CI, packaging, cleanup |
| `test/<short-name>`    | Tests only                      |

Examples: `feature/hot-reload-rules`, `fix/treasury-withdrawal-flag`, `docs/contributing-guidelines`.

Avoid long-lived personal branches; prefer rebasing onto latest `main` before opening or updating a PR.

## Commit Messages

Follow [Conventional Commits](https://www.conventionalcommits.org/)-style messages:

```
<type>(optional-scope): <short summary>

[optional body]
```

Common types: `feat`, `fix`, `docs`, `test`, `refactor`, `chore`, `ci`.

Guidelines:

- Use the imperative mood (“add”, “fix”, not “added” / “fixes”)
- Keep the summary ≤ ~72 characters
- Explain _why_ in the body when the change is non-obvious
- Reference issues when applicable (`Fixes #123`)

Examples:

```
fix(governance): detect treasury withdrawal proposals from Ogmios

feat(engine): add optional policy-id denylist
docs: expand contribution guidelines
```

## Issue Reporting

### Bug report

Include:

- **Summary** — what broke
- **Steps to reproduce**
- **Expected vs actual behaviour**
- **Environment** — OS, .NET SDK version, Docker/Compose versions, network (mainnet/preprod), Ogmios version/host if relevant
- **Logs / screenshots** — redact secrets and connection credentials
- **Commit / tag** — e.g. `v1.0.1` or a SHA

### Feature request

Include:

- **Problem** — what you are trying to do
- **Proposed approach** (optional)
- **Alternatives considered**
- **Impact** — who benefits (rule authors, UI consumers, operators)

## Pull Requests

### Before you open a PR

- [ ] Branch is up to date with `main`
- [ ] `dotnet build` succeeds
- [ ] `dotnet test` passes
- [ ] UI changes: `npm test` / lint in `tools/event-viewer` if applicable
- [ ] Docs / examples updated when behaviour or config changes
- [ ] No secrets, personal endpoints, or `.env` files committed

### PR description

Use this structure (also available as a GitHub PR template):

```markdown
## Summary

- What changed and why

## Test plan

- [ ] Unit tests added/updated
- [ ] Manual verification steps (if any)

## Notes

Breaking changes, follow-ups, or related issues
```

### Review checklist (maintainers & reviewers)

- [ ] Scope is focused; unrelated refactors are separate
- [ ] Public APIs / CloudEvents schema changes are intentional and documented
- [ ] Rule/config changes have tests and example `appsettings` updates when needed
- [ ] Error handling and logging are appropriate (no sensitive data)
- [ ] CI (build, tests, format, CodeQL) is green

Prefer small, reviewable PRs. Large features can be split into stacked PRs with a linking issue.

## Adding a New Rule

1. Create a new class in `src/BlockchainEvents.Engine/Rules/`
2. Inherit from `TransactionRuleBase` or implement `ITransactionRule`
3. Add configuration options under `Rules:<Name>` in `appsettings.json` (and optionally an `examples/` profile)
4. Bind and register the rule in `Program.cs` via `Configure<TOptions>(GetSection(...))`
5. Add unit tests in `tests/BlockchainEvents.Tests/Rules/`

See [docs/integration-guide.md](docs/integration-guide.md) for detailed examples and [docs/getting-started.md](docs/getting-started.md) for onboarding.

## Testing

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test class
dotnet test --filter "FullyQualifiedName~AddressMatchRuleTests"
```

## Security / Responsible Disclosure

Do **not** open a public issue for security vulnerabilities that could be exploited. Report privately via a [GitHub Security Advisory](https://docs.github.com/en/code-security/security-advisories/guidance-on-reporting-and-writing-information-about-vulnerabilities/privately-reporting-a-security-vulnerability) on this repository, or contact the maintainer via GitHub ([ItsDaveB](https://github.com/ItsDaveB)), with steps to reproduce and impact assessment. We will acknowledge and work on a fix before any public disclosure.

## License

By contributing, you agree that your contributions will be licensed under the MIT License.
