# Smart-X IoT Mesh Gateway — Part 1

📺 Demo video: https://youtu.be/y5HmTqGUoi8

Part 1 of the Smart-X IoT Mesh Ecosystem: the Sensor Data Ingestion and Validation Gateway. A simulated network of ESP32-style nodes (hydroponic moisture sensors, smart power meters, actuators) registers itself against a central .NET gateway, which validates each device's deployment location, accepts config/log/photo attachments, and drives a live "Mesh Pulse" dashboard widget.

Two more parts build directly on top of this one later in the semester
(real-time command streaming in Part 2, mesh graph routing in the final
PoE) — the navigation menu already shows those as locked pillars.

## Architecture

```
SmartX.sln
  SmartX.Shared/   Class library - models, TelemetryPacket<T>, operator
                    overloads, the recursive deployment validator, and the
                    jagged-array -> List<T> batch processor. Referenced by
                    every other project so client and API share one
                    definition of every type that crosses the wire.
  SmartX.Api/       ASP.NET Core Minimal API - in-memory gateway state,
                    file uploads, the live telemetry pulse generator.
  SmartX.Client/    Blazor WebAssembly dashboard - talks to the API over
                    plain HTTP via a typed HttpClient, nothing in-process.
  SmartX.Tests/     xUnit tests for the Shared library's data structures
                    and algorithms.
```

- **Backend:** ASP.NET Core Minimal API on .NET 10
- **Frontend:** Blazor WebAssembly (standalone), .NET 10
- **State:** in-memory for Part 1 (seeded mock data), per the assessment's
  simulation note — no database is required at this stage.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- (Optional) [Docker Desktop](https://www.docker.com/products/docker-desktop/) if you want to run the containerised version instead

Check your SDK version:

```bash
dotnet --version   # should report 10.x
```

## Running locally (no Docker)

Restore and build everything from the solution root:

```bash
dotnet restore
dotnet build
```

**1. Start the API** (in one terminal):

```bash
dotnet run --project SmartX.Api --urls http://localhost:5074
```

The API listens on `http://localhost:5074` and seeds itself with mock
sensors, a deployment tree, and jagged-batch telemetry history on startup —
nothing further to configure.

**2. Start the Blazor client** (in a second terminal):

```bash
dotnet run --project SmartX.Client --urls http://localhost:5219
```

Open `http://localhost:5219` in a browser. The client is already configured
(`SmartX.Client/wwwroot/appsettings.json`) to call the API at
`http://localhost:5074/`.

> Both dev-server ports are pre-registered in the API's CORS policy
> (`SmartX.Api/Program.cs`). If you change either port, update the
> corresponding `WithOrigins(...)` list and `ApiBaseUrl` value.

## Running with Docker

```bash
docker compose up --build
```

- API → `http://localhost:8080`
- Client → `http://localhost:8081`

`docker-compose.yml` builds both images from the multi-stage Dockerfiles in
`SmartX.Api/` and `SmartX.Client/` (the client is published as static files
served by nginx). The client's `appsettings.Production.json` points it at
`http://localhost:8080/` for this setup.

## Running the tests

```bash
dotnet test
```

Covers the recursive deployment-tree validator (valid/broken/arbitrary-depth
chains), the `PowerReading`/`MoistureReading` operator overloads, the jagged
array → `List<T>` batch processor, and the custom `SensorLedger` collection.

## Using the app

1. Open the client — the landing page shows the three architectural pillars.
   Only **Sensor Data Ingestion & Telemetry** is active in Part 1; the other
   two are visible but link to a "coming soon" page.
2. **Register Sensor** tab: pick a MAC address, a category, and drill down
   through the deployment tree (Facility → Zone → Sub-Zone → ... → Node).
   The path is validated recursively against the site hierarchy server-side
   — try picking a path that doesn't exist to see the validation error.
3. **Sensor Registry** tab: expand any row to attach a config file,
   deployment photo, or hardware log, and to download files already
   attached.
4. **Live Mesh Pulse** (top of the page): a live, animated widget — not a
   progress bar — that polls the API every 2 seconds and pulses each
   registered sensor's node according to its current simulated reading and
   anomaly score.

## Where each technical requirement lives

| Requirement | Location |
|---|---|
| Generic `TelemetryPacket<T>` | `SmartX.Shared/Models/TelemetryPacket.cs` |
| Operator overloading | `SmartX.Shared/Models/PowerReading.cs`, `MoistureReading.cs` |
| Jagged/multi-dimensional arrays → `List<T>` | `SmartX.Shared/Processing/TelemetryBatchProcessor.cs`, seeded in `SmartX.Api/Services/TelemetryHistoryStore.cs` |
| Recursive deployment validator | `SmartX.Shared/Validation/DeploymentTreeValidator.cs` |
| Custom collection | `SmartX.Shared/Collections/SensorLedger.cs` |
| Dynamic engagement feature | `SmartX.Api/Services/TelemetryPulseGenerator.cs` + `SmartX.Client/Components/MeshPulseWidget.razor` |
| File upload | `SmartX.Api/Services/AttachmentStorage.cs`, `SmartX.Client/Components/SensorRegistry.razor` |

## API reference

See `SmartX.Api/SmartX.Api.http` for ready-to-run manual requests (works
directly in VS Code / Rider / Visual Studio's HTTP client), or hit
`http://localhost:5074/openapi/v1.json` while the API is running in
Development mode.

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/deployment-tree` | Full facility/zone/sub-zone/node hierarchy |
| GET | `/api/sensors` | List registered sensors |
| GET | `/api/sensors/{mac}` | Fetch one sensor |
| POST | `/api/sensors` | Register a sensor (validates deployment path) |
| POST | `/api/sensors/{mac}/attachments` | Upload a config/photo/log (multipart) |
| GET | `/api/sensors/{mac}/attachments/{id}/download` | Download an attachment |
| GET | `/api/telemetry/pulses` | Live simulated readings (Mesh Pulse feed) |
| GET | `/api/telemetry/history/{sensorId}?take=50` | Flattened historical readings |

## Notes

- All gateway state is in-memory and resets on API restart — expected for
  this simulation stage; a persistence layer isn't part of Part 1's scope.
- Uploaded files are written to `SmartX.Api/UploadedFiles/` (git-ignored).
