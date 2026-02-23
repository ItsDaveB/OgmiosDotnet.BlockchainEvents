FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/BlockchainEvents.Domain/BlockchainEvents.Domain.csproj src/BlockchainEvents.Domain/
COPY tools/BlockchainEvents.DemoSubscriber/BlockchainEvents.DemoSubscriber.csproj tools/BlockchainEvents.DemoSubscriber/
RUN dotnet restore tools/BlockchainEvents.DemoSubscriber/BlockchainEvents.DemoSubscriber.csproj
COPY src/BlockchainEvents.Domain/ src/BlockchainEvents.Domain/
COPY tools/BlockchainEvents.DemoSubscriber/ tools/BlockchainEvents.DemoSubscriber/
RUN dotnet publish tools/BlockchainEvents.DemoSubscriber/BlockchainEvents.DemoSubscriber.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS debug
WORKDIR /src
COPY src/BlockchainEvents.Domain/BlockchainEvents.Domain.csproj src/BlockchainEvents.Domain/
COPY tools/BlockchainEvents.DemoSubscriber/BlockchainEvents.DemoSubscriber.csproj tools/BlockchainEvents.DemoSubscriber/
RUN dotnet restore tools/BlockchainEvents.DemoSubscriber/BlockchainEvents.DemoSubscriber.csproj
COPY src/BlockchainEvents.Domain/ src/BlockchainEvents.Domain/
COPY tools/BlockchainEvents.DemoSubscriber/ tools/BlockchainEvents.DemoSubscriber/
RUN dotnet publish tools/BlockchainEvents.DemoSubscriber/BlockchainEvents.DemoSubscriber.csproj -c Debug -o /app
RUN dotnet tool install --tool-path /vsdbg vsdbg 2>/dev/null; \
    curl -sSL https://aka.ms/getvsdbgsh | bash /dev/stdin -v latest -l /vsdbg

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./
ENV ASPNETCORE_URLS=http://+:4001
EXPOSE 4001
ENTRYPOINT ["dotnet", "BlockchainEvents.DemoSubscriber.dll"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime-debug
WORKDIR /app
COPY --from=debug /app ./
COPY --from=debug /vsdbg /vsdbg
ENV ASPNETCORE_URLS=http://+:4001
EXPOSE 4001
ENTRYPOINT ["dotnet", "BlockchainEvents.DemoSubscriber.dll"]
