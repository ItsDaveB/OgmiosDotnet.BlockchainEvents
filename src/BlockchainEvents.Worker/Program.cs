var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<BlockchainEventsOptions>(
    builder.Configuration.GetSection(BlockchainEventsOptions.SectionName));
builder.Services.Configure<OgmiosOptions>(
    builder.Configuration.GetSection(OgmiosOptions.SectionName));

builder.Services.AddDaprClient();

builder.Services.Configure<MetadataKeyValueRuleOptions>(opts =>
{
    opts.Enabled = true;
    opts.Labels = [674]; // CIP-20 transaction messages
});
builder.Services.AddSingleton<ITransactionRule, MetadataKeyValueRule>();

builder.Services.Configure<GovernanceTreasuryRuleOptions>(opts =>
{
    opts.Enabled = true;
    opts.IncludeGovernanceActions = true;
    opts.IncludeTreasuryWithdrawals = true;
    opts.IncludeDelegations = true;
    opts.IncludeStakeRegistrations = true;
    opts.IncludeVotes = true;
});
builder.Services.AddSingleton<ITransactionRule, GovernanceTreasuryRule>();

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
