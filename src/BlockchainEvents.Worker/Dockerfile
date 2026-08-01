FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Distro protoc avoids Grpc.Tools bundled linux_arm64 protoc SIGSEGV in some environments.
RUN apt-get update \
    && apt-get install -y --no-install-recommends protobuf-compiler \
    && rm -rf /var/lib/apt/lists/*

COPY OgmiosDotnet.BlockchainEvents.sln ./
COPY Directory.Build.props ./
COPY src/BlockchainEvents.Domain/BlockchainEvents.Domain.csproj src/BlockchainEvents.Domain/
COPY src/BlockchainEvents.Engine/BlockchainEvents.Engine.csproj src/BlockchainEvents.Engine/
COPY src/BlockchainEvents.Worker/BlockchainEvents.Worker.csproj src/BlockchainEvents.Worker/

RUN dotnet restore src/BlockchainEvents.Worker/BlockchainEvents.Worker.csproj

COPY protos/ protos/
COPY README.md ./
COPY src/ src/

RUN dotnet publish src/BlockchainEvents.Worker/BlockchainEvents.Worker.csproj \
    -c Release \
    -o /app/publish \
    -p:Protobuf_ProtocFullPath=/usr/bin/protoc

FROM build AS debug
RUN dotnet publish src/BlockchainEvents.Worker/BlockchainEvents.Worker.csproj \
    -c Debug \
    -o /app/debug-publish \
    -p:Protobuf_ProtocFullPath=/usr/bin/protoc

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN groupadd -r appgroup && useradd -r -g appgroup appuser

COPY --from=build /app/publish .

RUN chown -R appuser:appgroup /app

USER appuser

ENV ASPNETCORE_URLS="http://+:4000;http://+:4010"
EXPOSE 4000 4010

ENTRYPOINT ["dotnet", "BlockchainEvents.Worker.dll"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime-debug
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends \
    ca-certificates curl unzip \
    && update-ca-certificates \
    && curl -sSL https://aka.ms/getvsdbgsh | bash /dev/stdin -v latest -l /vsdbg \
    && rm -rf /var/lib/apt/lists/*

COPY --from=debug /app/debug-publish .

ENV ASPNETCORE_URLS="http://+:4000;http://+:4010"
EXPOSE 4000 4010

ENTRYPOINT ["dotnet", "BlockchainEvents.Worker.dll"]
