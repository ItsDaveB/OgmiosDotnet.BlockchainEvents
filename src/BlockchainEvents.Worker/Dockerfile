FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files
COPY OgmiosDotnet.BlockchainEvents.sln ./
COPY src/BlockchainEvents.Domain/BlockchainEvents.Domain.csproj src/BlockchainEvents.Domain/
COPY src/BlockchainEvents.Engine/BlockchainEvents.Engine.csproj src/BlockchainEvents.Engine/
COPY src/BlockchainEvents.Worker/BlockchainEvents.Worker.csproj src/BlockchainEvents.Worker/

# Restore dependencies
RUN dotnet restore src/BlockchainEvents.Worker/BlockchainEvents.Worker.csproj

# Copy source code
COPY src/ src/

# Build and publish
RUN dotnet publish src/BlockchainEvents.Worker/BlockchainEvents.Worker.csproj \
    -c Release \
    -o /app/publish

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Install CA certificates for SSL/TLS connections
RUN apt-get update && apt-get install -y --no-install-recommends \
    ca-certificates \
    && update-ca-certificates \
    && rm -rf /var/lib/apt/lists/*

# Create non-root user for security
RUN groupadd -r appgroup && useradd -r -g appgroup appuser

# Copy published application
COPY --from=build /app/publish .

# Set ownership
RUN chown -R appuser:appgroup /app

# Switch to non-root user
USER appuser

# Health check endpoint (Dapr sidecar handles app health)
ENV ASPNETCORE_URLS=http://+:5050
EXPOSE 5050

ENTRYPOINT ["dotnet", "BlockchainEvents.Worker.dll"]
