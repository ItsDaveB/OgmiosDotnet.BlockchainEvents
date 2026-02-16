var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<BlockchainEventsOptions>(
    builder.Configuration.GetSection(BlockchainEventsOptions.SectionName));
builder.Services.Configure<OgmiosConfiguration>(
    builder.Configuration.GetSection(OgmiosConfiguration.SectionName));

builder.Services.AddDaprClient();

// Transaction rules - add or remove rules here
builder.Services.Configure<MetadataKeyValueRuleOptions>(opts =>
{
    opts.Enabled = true;
    opts.Labels = [674]; // CIP-20 transaction messages
});
builder.Services.AddSingleton<ITransactionRule, MetadataKeyValueRule>();

builder.Services.AddSingleton<IRuleEngine, RuleEngine>();
builder.Services.AddSingleton<IEventMetrics, EventMetrics>();
builder.Services.AddSingleton<IBlockchainEventEmitter, DaprEventEmitter>();
builder.Services.AddSingleton<ICheckpointService, DaprCheckpointService>();

builder.Services.AddOgmiosServices();

builder.Services.AddSingleton<OgmiosChainSyncAdapter>();
builder.Services.AddSingleton<IChainSyncService>(sp => sp.GetRequiredService<OgmiosChainSyncAdapter>());
builder.Services.AddSingleton<Ogmios.Services.ChainSynchronization.IChainSynchronizationMessageHandlers>(
    sp => sp.GetRequiredService<OgmiosChainSyncAdapter>());

builder.Services.AddHostedService<BlockchainEventsWorker>();

var app = builder.Build();

app.MapSubscriptionEndpoints();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTimeOffset.UtcNow }));
app.MapMetrics(); // Prometheus metrics endpoint at /metrics

await app.RunAsync();
