var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<BlockchainEventsOptions>(
    builder.Configuration.GetSection(BlockchainEventsOptions.SectionName));
builder.Services.AddOptions<OgmiosOptions>()
    .Bind(builder.Configuration.GetSection(OgmiosOptions.SectionName))
    .Validate(opts => opts.Connection.Host != "localhost" || !opts.Connection.Tls,
        "Ogmios connection still has defaults (localhost + TLS). Check that the Ogmios section in appsettings.json is loading correctly.")
    .ValidateOnStart();

builder.Services.AddDaprClient();

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

builder.Services.AddSingleton<IRuleEngine, RuleEngine>();
builder.Services.AddSingleton<IEventMetrics, EventMetrics>();
builder.Services.AddSingleton<IBlockchainEventEmitter, DaprEventEmitter>();
builder.Services.AddSingleton<ICheckpointService, DaprCheckpointService>();

builder.Services.AddOgmiosServices();

builder.Services.ConfigureHttpClientDefaults(http =>
    http.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = (_, _, _, errors) =>
            errors is System.Net.Security.SslPolicyErrors.None
                    or System.Net.Security.SslPolicyErrors.RemoteCertificateNameMismatch
    }));

builder.Services.AddSingleton<OgmiosChainSyncAdapter>();
builder.Services.AddSingleton<IChainSyncService>(sp => sp.GetRequiredService<OgmiosChainSyncAdapter>());
builder.Services.AddSingleton<Ogmios.Services.ChainSynchronization.IChainSynchronizationMessageHandlers>(
    sp => sp.GetRequiredService<OgmiosChainSyncAdapter>());

builder.Services.AddHostedService<BlockchainEventsWorker>();

var app = builder.Build();

app.MapSubscriptionEndpoints();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTimeOffset.UtcNow }));
app.MapMetrics();

await app.RunAsync();
