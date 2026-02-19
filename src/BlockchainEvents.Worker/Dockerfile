FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY OgmiosDotnet.BlockchainEvents.sln ./
COPY src/BlockchainEvents.Domain/BlockchainEvents.Domain.csproj src/BlockchainEvents.Domain/
COPY src/BlockchainEvents.Engine/BlockchainEvents.Engine.csproj src/BlockchainEvents.Engine/
COPY src/BlockchainEvents.Worker/BlockchainEvents.Worker.csproj src/BlockchainEvents.Worker/

RUN dotnet restore src/BlockchainEvents.Worker/BlockchainEvents.Worker.csproj

COPY src/ src/

RUN dotnet publish src/BlockchainEvents.Worker/BlockchainEvents.Worker.csproj \
    -c Release \
    -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends \
    ca-certificates \
    && update-ca-certificates \
    && rm -rf /var/lib/apt/lists/*

RUN groupadd -r appgroup && useradd -r -g appgroup appuser

COPY --from=build /app/publish .

RUN chown -R appuser:appgroup /app

USER appuser

ENV ASPNETCORE_URLS=http://+:5050
EXPOSE 5050

ENTRYPOINT ["dotnet", "BlockchainEvents.Worker.dll"]
