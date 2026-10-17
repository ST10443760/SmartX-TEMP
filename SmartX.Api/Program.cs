using SmartX.Api.Data;
using SmartX.Api.Endpoints;
using SmartX.Api.Hubs;
using SmartX.Api.Services;
using SmartX.Shared.Collections;
using SmartX.Shared.Realtime;

var builder = WebApplication.CreateBuilder(args);

const string ClientCorsPolicy = "SmartXClient";

builder.Services.AddCors(options =>
{
    options.AddPolicy(ClientCorsPolicy, policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:5219", "https://localhost:7036", // SmartX.Client (Kestrel dev server)
                "http://localhost:8081") // SmartX.Client (docker-compose / nginx)
            .AllowAnyHeader()
            .AllowAnyMethod()
            // SignalR negotiation sends credentials from the browser, which
            // CORS only allows alongside an explicit origin list (above).
            .AllowCredentials();
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddSignalR();

// Gateway state - in-memory for Part 1, per the assessment's simulation note.
builder.Services.AddSingleton(DeploymentTreeSeed.BuildDefaultSite());
builder.Services.AddSingleton<SensorLedger>(_ =>
{
    var ledger = new SensorLedger();
    SensorSeed.Populate(ledger);
    return ledger;
});
builder.Services.AddSingleton<AttachmentStorage>();
builder.Services.AddSingleton<TelemetryHistoryStore>(sp =>
{
    var store = new TelemetryHistoryStore();
    var ledger = sp.GetRequiredService<SensorLedger>();

    foreach (var sensor in ledger)
    {
        store.SeedFromJaggedBatches(
            sensor.MacAddress,
            unit: sensor.Category.ToString() == "PowerConsumption" ? "W" : "%",
            burstCount: 12,
            minPerBurst: 3,
            maxPerBurst: 9,
            baseline: 45,
            noise: 6);
    }

    return store;
});

// Background service that keeps the live dashboard pulse feed moving; also
// registered as itself so endpoints can read the latest Snapshot() directly.
builder.Services.AddSingleton<TelemetryPulseGenerator>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<TelemetryPulseGenerator>());

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors(ClientCorsPolicy);

app.MapGet("/", () => Results.Ok(new { service = "SmartX.Api", status = "running" }));

app.MapDeploymentEndpoints();
app.MapSensorEndpoints();
app.MapTelemetryEndpoints();

app.MapHub<TelemetryHub>(HubEvents.HubRoute);

app.Run();
