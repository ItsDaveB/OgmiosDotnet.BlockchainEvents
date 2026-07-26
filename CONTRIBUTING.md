# Contributing to OgmiosDotnet.BlockchainEvents

Thank you for your interest in contributing! This document provides guidelines for contributing to the project.

## Development Setup

### Prerequisites

- .NET 10 SDK
- Docker & Docker Compose
- Git

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
```

## Adding a New Rule

1. Create a new class in `src/BlockchainEvents.Engine/Rules/`
2. Inherit from `TransactionRuleBase` or implement `ITransactionRule`
3. Add configuration options under `Rules:<Name>` in `appsettings.json` (and optionally an `examples/` profile)
4. Bind and register the rule in `Program.cs` via `Configure<TOptions>(GetSection(...))`
5. Add unit tests in `tests/BlockchainEvents.Tests/Rules/`

See [docs/integration-guide.md](docs/integration-guide.md) for detailed examples and [docs/getting-started.md](docs/getting-started.md) for onboarding.

## Testing

### Running Tests

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test class
dotnet test --filter "FullyQualifiedName~AddressMatchRuleTests"
```

## License

By contributing, you agree that your contributions will be licensed under the MIT License.
