# Contributing to OgmiosDotnet.BlockchainEvents

Thank you for your interest in contributing! This document provides guidelines for contributing to the project.

## Development Setup

### Prerequisites

- .NET 9 SDK
- Docker & Docker Compose
- Git

### Getting Started

```bash
# Clone the repository
git clone https://github.com/your-org/OgmiosDotnet.BlockchainEvents.git
cd OgmiosDotnet.BlockchainEvents

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
├── architecture.md            # System architecture
├── event-schema.md            # Event schema specification
└── integration-guide.md       # Custom rule development guide
```

## Adding a New Rule

1. Create a new class in `src/BlockchainEvents.Engine/Rules/`
2. Inherit from `TransactionRuleBase` or implement `ITransactionRule`
3. Add configuration options if needed
4. Register the rule in `Program.cs`
5. Add unit tests in `tests/BlockchainEvents.Tests/Rules/`

See [docs/integration-guide.md](docs/integration-guide.md) for detailed examples.

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
