var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<BlockchainEventsOptions>(
    builder.Configuration.GetSection(BlockchainEventsOptions.SectionName));
builder.Services.AddOptions<OgmiosOptions>()
    .Bind(builder.Configuration.GetSection(OgmiosOptions.SectionName))
    .Validate(opts => opts.Connection.Host != "localhost" || !opts.Connection.Tls,
        "Ogmios connection still has defaults (localhost + TLS). Check that the Ogmios section in appsettings.json is loading correctly.")
    .ValidateOnStart();

builder.Services.AddDaprClient();
builder.Services.AddGrpc();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi("v1", options =>
{
    options.AddDocumentTransformer((document, context, ct) =>
    {
        document.Info.Title = "OgmiosDotnet.BlockchainEvents API";
        document.Info.Version = "v1";
        document.Info.Description = "Real-time Cardano blockchain event filtering and delivery via CloudEvents 1.0. " +
                      "Supports HTTP (Dapr pub/sub) and gRPC streaming consumption.";
        return Task.CompletedTask;
    });
});

// Rule 1: Minswap V2 DEX — match batched swap orders by contract address prefix.
// The order script hash c3e28c36c3447315ba5a56f33da6a6ddc1770a876a8d9f0cb3a97c4c
// produces bech32 prefixes addr1z (script+key staking), addr1x (script+script staking),
// and addr1w (enterprise / no staking). Derived from the official order enterprise address:
// addr1w8p79rpkcdz8x9d6tft0x0dx5mwuzac2sa4gm8cvkw5hcnqst2ctf
builder.Services.Configure<AddressMatchRuleOptions>(opts =>
{
    opts.Enabled = true;
    opts.Prefixes =
    [
        "addr1z8p79rpkcdz8x9d6tft0x0dx5mwuzac2sa4gm8cvkw5hc",
        "addr1x8p79rpkcdz8x9d6tft0x0dx5mwuzac2sa4gm8cvkw5hc",
        "addr1w8p79rpkcdz8x9d6tft0x0dx5mwuzac2sa4gm8cvkw5hc"
    ];
});
builder.Services.AddSingleton<ITransactionRule, AddressMatchRule>();

// Rule 2: Governance votes — match only on-chain voting transactions.
builder.Services.Configure<GovernanceTreasuryRuleOptions>(opts =>
{
    opts.Enabled = true;
    opts.IncludeGovernanceActions = false;
    opts.IncludeTreasuryWithdrawals = false;
    opts.IncludeDelegations = false;
    opts.IncludeStakeRegistrations = false;
    opts.IncludeVotes = true;
});
builder.Services.AddSingleton<ITransactionRule, GovernanceTreasuryRule>();

// Rule 3: Any transaction with metadata — match all metadata regardless of label.
builder.Services.Configure<MetadataKeyValueRuleOptions>(opts =>
{
    opts.Enabled = true;
    opts.MatchAny = true;
});
builder.Services.AddSingleton<ITransactionRule, MetadataKeyValueRule>();

// Rule 4: All transactions — emit an event for every transaction (demo/benchmarking).
builder.Services.Configure<AllTransactionsRuleOptions>(opts =>
{
    opts.Enabled = true;
});
builder.Services.AddSingleton<ITransactionRule, AllTransactionsRule>();

builder.Services.AddSingleton<IRuleEngine, RuleEngine>();
builder.Services.AddSingleton<IEventMetrics, EventMetrics>();
builder.Services.AddSingleton<IEventBroadcaster, EventBroadcaster>();
builder.Services.AddSingleton<IBlockchainEventEmitter, DaprEventEmitter>();
builder.Services.AddSingleton<ICheckpointService, DaprCheckpointService>();

builder.Services.AddOgmiosServices();

builder.Services.ConfigureHttpClientDefaults(http =>
    http.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
        {
            // Demeter hosted Ogmios endpoints use a proxy whose TLS certificate
            // does not match the per-tenant hostname, producing NameMismatch.
            // Allow connections when NameMismatch is the only issue.
            if (errors == System.Net.Security.SslPolicyErrors.None)
                return true;
            return errors == System.Net.Security.SslPolicyErrors.RemoteCertificateNameMismatch;
        }
    }));

builder.Services.AddSingleton<OgmiosChainSyncAdapter>();
builder.Services.AddSingleton<IChainSyncService>(sp => sp.GetRequiredService<OgmiosChainSyncAdapter>());
builder.Services.AddSingleton<Ogmios.Services.ChainSynchronization.IChainSynchronizationMessageHandlers>(
    sp => sp.GetRequiredService<OgmiosChainSyncAdapter>());

builder.Services.AddHostedService<BlockchainEventsWorker>();
builder.Services.AddCors(options =>
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseCors("AllowAll");
app.MapOpenApi();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/openapi/v1.json", "BlockchainEvents API v1"));

app.MapGrpcService<BlockchainEventGrpcService>();
app.MapSubscriptionEndpoints();
app.MapSseEndpoints();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTimeOffset.UtcNow }))
    .WithName("HealthCheck")
    .WithTags("Health")
    .WithDescription("Returns the current health status of the service.");
app.MapMetrics();

await app.RunAsync();
