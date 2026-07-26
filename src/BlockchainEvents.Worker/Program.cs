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

// Built-in rules bind from the Rules:* sections in appsettings.json (or example overlays).
// Defaults match the Minswap / governance-votes / metadata / all-transactions demo stack.
// See examples/ for governance, treasury, and metadata-focused configurations.
builder.Services.Configure<AddressMatchRuleOptions>(
    builder.Configuration.GetSection(AddressMatchRuleOptions.SectionName));
builder.Services.AddSingleton<ITransactionRule, AddressMatchRule>();

builder.Services.Configure<GovernanceTreasuryRuleOptions>(
    builder.Configuration.GetSection(GovernanceTreasuryRuleOptions.SectionName));
builder.Services.AddSingleton<ITransactionRule, GovernanceTreasuryRule>();

builder.Services.Configure<MetadataKeyValueRuleOptions>(
    builder.Configuration.GetSection(MetadataKeyValueRuleOptions.SectionName));
builder.Services.AddSingleton<ITransactionRule, MetadataKeyValueRule>();

builder.Services.Configure<PolicyIdAssetRuleOptions>(
    builder.Configuration.GetSection(PolicyIdAssetRuleOptions.SectionName));
builder.Services.AddSingleton<ITransactionRule, PolicyIdAssetRule>();

builder.Services.Configure<AllTransactionsRuleOptions>(
    builder.Configuration.GetSection(AllTransactionsRuleOptions.SectionName));
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
if (Environment.GetEnvironmentVariable("DEMO_EVENTS") == "true")
    builder.Services.AddHostedService<DemoEventSeeder>();

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
