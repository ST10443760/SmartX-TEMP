# Smart-X IoT Mesh Gateway - Parts 1 & 2

📺 Part 1 demo video: https://youtu.be/y5HmTqGUoi8

Smart-X is a simulated IoT mesh ecosystem: ESP32-style nodes (hydroponic
moisture sensors, smart power meters, pumps, valves and relays) register
with a central .NET gateway and stream telemetry to it.

- **Part 1 - Sensor Data Ingestion & Telemetry.** Registers sensors,
  validates their deployment location recursively, accepts config/log/photo
  attachments and drives the live "Mesh Pulse" widget.
- **Part 2 - Real-Time Command Stream & History.** Turns the gateway into a
  real-time processing engine:
  - packets go through a FIFO queue and a priority queue for critical alerts;
  - manual overrides sit on an undo stack;
  - live devices live in a MAC-keyed dictionary, with per-device history in a
    `SortedDictionary`;
  - a `HashSet` de-duplicates alerts;
  - an Automated Action Engine learns what operators usually do next.
  Everything is pushed to the dashboard over SignalR.

The third pillar (Network Topology & Mesh Routing) is part of the final PoE.
It is visible in the menu but disabled.

## Architecture

```
SmartX.slnx
  SmartX.Shared/     Class library shared by every project.
    Models/, Dtos/     Types that cross the wire (TelemetryPacket<T>, LiveReading, ...)
    Collections/       Part 1 SensorLedger (List<T>-backed custom collection)
    Validation/        Part 1 recursive deployment-tree validator
    Processing/        Part 1 jagged array -> List<T> batch processor
    Realtime/          Part 2 queues, device dictionary, sorted history, alert set, disconnect detector
    Commands/          Part 2 override command stack, actuator board, command processor (undo)
    Insights/          Part 2 suggestion engine + device health analyzer
  SmartX.Api/        ASP.NET Core Minimal API + SignalR hub + background workers
  SmartX.Client/     Blazor WebAssembly dashboard (talks to the API over HTTP + SignalR only)
  SmartX.Simulator/  Console app: a fake device fleet that registers and POSTs telemetry over HTTP
  SmartX.Tests/      xUnit tests for every data structure and the suggestion engine
```

- **Backend:** ASP.NET Core Minimal API on .NET 10, SignalR for live push.
- **Frontend:** Blazor WebAssembly (standalone), .NET 10.
- **State:** in memory, which is enough for the simulation brief. It resets
  when the API restarts.

### How a packet flows (Part 2)

```
Simulator / device ──POST /api/telemetry/ingest──► dictionary lookup (unknown MAC → 404)
                                                    │
                     Warning/Critical ──► CriticalAlertQueue (PriorityQueue, severity + counter)
                     Normal ───────────► PacketQueue (Queue<T>, FIFO)
                                                    │  202 Accepted returned immediately
TelemetryProcessingWorker (BackgroundService) ◄─────┘  drains priority queue FIRST, then FIFO
   ├─ DeviceDirectory (Dictionary by MAC) ── latest value
   ├─ ReadingHistoryStore (SortedDictionary<DateTime,…> per device, capped) ── timeline chart
   ├─ ActiveAlertSet (HashSet) ── raise once / suppress duplicate / recover
   ├─ InsightsService ── records device conditions for the action engine
   └─ SignalR hub ──► every open dashboard (telemetry, alerts, commands, queue stats, insights)
```

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- (Optional) [Docker Desktop](https://www.docker.com/products/docker-desktop/) for the containerised version

```bash
dotnet --version   # should report 10.x
```

## Restore, build and test

From the solution root:

```bash
dotnet restore
dotnet build
dotnet test
```

## Running locally (no Docker) - three terminals

You need **two terminals** for the app itself (API + client) and **a third,
optional one** for the device simulator. Without the simulator the dashboard
still works. The seeded devices just won't stream telemetry, but the
**Simulate critical alert** and **Simulate disconnect** buttons still
exercise the queues and the alert set.

**Terminal 1 - API** (start this first):

```bash
dotnet run --project SmartX.Api --urls http://localhost:5074
```

**Terminal 2 - Blazor client:**

```bash
dotnet run --project SmartX.Client --urls http://localhost:5219
```

Open `http://localhost:5219`. The client is configured in
`SmartX.Client/wwwroot/appsettings.json` to call the API at
`http://localhost:5074/`. The SignalR hub URL is built from the same setting.

**Terminal 3 - simulator** (optional, recommended for the demo):

```bash
dotnet run --project SmartX.Simulator -- --devices 6 --interval 2
```

The simulator registers 6 extra devices through the Part 1 registration
endpoint (`POST /api/sensors`). It then sends one packet per device every 2
seconds to `POST /api/telemetry/ingest`, and it also drives the 10 devices
the API seeds at startup. Each device type sends its native value type:

| Device | Wire value | Example |
|---|---|---|
| Moisture sensor | `float` | `{ "macAddress": "...", "moisture": 44.5 }` |
| Power meter | `int` | `{ "macAddress": "...", "watts": 612 }` |
| Pump / valve / relay | `bool` | `{ "macAddress": "...", "valveOpen": true }` |

Keys while the simulator runs:

| Key | Effect |
|---|---|
| `c` | Send a critical spike (moisture 6.5% or ~4300 W) for a random sensor on the next round |
| `d` | Silence a random sensor (it stops sending - the gateway flags it once after the timeout) |
| `r` | Reconnect every silenced device (the gateway raises "recovered" events) |
| `q` | Quit |

Flags (also available as `SIM_*` environment variables): `--api <url>`,
`--devices <n>`, `--interval <seconds>`, `--no-seeded`, `--spike`,
`--spike-every <seconds>`, `--disconnect <mac|name>` (silences that device
after round 3). Run `dotnet run --project SmartX.Simulator -- --help` for
the full list.

> Both client dev-server ports (5219, 7036) and the Docker client port
> (8081) are in the API's CORS policy (`SmartX.Api/Program.cs`). If you
> change a port, update `WithOrigins(...)` and `ApiBaseUrl`.

## Running with Docker

```bash
docker compose up --build                      # API + client
docker compose --profile simulator up --build  # API + client + simulator
```

- API: `http://localhost:8080`
- Client: `http://localhost:8081`

The client is published as static files served by nginx, and its
`appsettings.Production.json` points at `http://localhost:8080/`.
Attachment download links and the SignalR hub URL both come from that one
setting.

The simulator service is optional (compose profile `simulator`). Inside the
compose network it reaches the API at `http://smartx-api:8080/`. Because a
container has no keyboard, it sends a critical spike every 45 seconds and
silences *Moisture Sensor D* after round 3. Both settings are in
`docker-compose.yml`. To use the keys, run
`docker attach smartx-smartx-simulator-1` and detach with `Ctrl+P, Ctrl+Q`.

## Using the app

1. **Home**: two active pillars (Telemetry, Command Stream) and the
   disabled Mesh Routing pillar. The top bar shows the live SignalR
   connection state on every page:
   - **Live**: connected;
   - **Reconnecting...**: the socket dropped and is retrying;
   - **Offline - polling**: the dashboard falls back to fetching
     `/api/stream/snapshot` every 3 seconds until the socket comes back.
2. **Sensor Telemetry (Part 1)**:
   - register a sensor (MAC, optional name, category, deployment path);
   - browse the registry and upload or download attachments;
   - watch the Mesh Pulse widget.
3. **Command Stream & History (Part 2)**:
   - **Queue stats bar:** FIFO and priority depths, packets processed,
     packets that jumped the queue, active alerts, duplicates suppressed and
     unknown MACs rejected. It also holds the **⚠ Simulate critical alert**
     button.
   - **Devices:** search by MAC or name, filter by category (Environmental /
     Power Consumption / Actuator) or *active alerts only*. Devices with
     alerts float to the top. Click a row to open its timeline.
   - **Device timeline:** an inline SVG chart of the selected device's sorted
     history, with warning and critical threshold lines and a hover readout.
     The **Simulate disconnect** button is here.
   - **Suggested Actions & Automated Insights:** one-click chips for the
     likely next action with a reason, plus devices flagged from the live
     data.
   - **Manual overrides:** trigger or stop the pump, toggle the relay, open a
     valve, **Close all valves**, **↶ Undo**, and the command stack.
   - **Alerts:** the active alert set plus raised / recovered / duplicate
     events.
   - **Live feed:** every processed packet with its processing order (`#`)
     and the lane it came through (FIFO / PRIORITY).

   Filters, the feed, the selected device, the alert list and the command
   history all live in a singleton `LiveStateContainer`. Moving to another
   page and back keeps everything.

## Testing the Part 2 structures

The steps below assume the API and client are running (Terminals 1 and 2)
and the **Command Stream & History** page is open. Every step also has a
ready-made request in `SmartX.Api/SmartX.Api.http`.

### 1. High-priority alert jumps the queue (Queue + PriorityQueue)

1. Click **⚠ Simulate critical alert** (top right of the stats bar).
   - Alternatively, call the API directly:
     `curl -X POST "http://localhost:5074/api/simulate/critical-alert?mac=24:6F:28:A1:00:02"`
2. The API queues **up to six routine packets first** and **one critical packet
   last**, in a single atomic batch.
3. In the **Live feed**, the critical packet has the lowest `#` number of
   the batch and a red **PRIORITY** badge. It was processed before the
   routine packets that arrived ahead of it. **Jumped the queue** goes up by
   one.
4. With the simulator running you can press `c` instead. The packet's 202
   response says `"lane":"Priority"`, and the simulator prints
   `1 via priority lane`.

### 2. Disconnect (HashSet + disconnect detector)

1. Select a device that has been reporting (e.g. *Moisture Sensor C*) in the
   device table.
2. Click **Simulate disconnect** under the timeline.
   - API alternative: `curl -X POST http://localhost:5074/api/simulate/disconnect/24:6F:28:A1:00:03`
3. A red **RAISED** event appears, the device shows **Disconnected**, the
   active alert set lists it once, and the nav menu shows the alert count.
4. Without the button: run the simulator with `--disconnect "Moisture Sensor C"`
   or press `d`. After `Gateway:DisconnectTimeoutSeconds` (10 s by default,
   in `SmartX.Api/appsettings.json`) of silence, the detector flags it
   automatically.

### 3. Duplicate disconnect is ignored

1. Click **Simulate disconnect** again on the same device (or run the curl
   command a second time).
2. The response says *"already in the disconnected set - duplicate ignored,
   no second alert"*. The event list shows a grey **DUPLICATE** entry, and
   **Duplicates suppressed** goes up.
3. The active alert set still contains the device **once**.
4. A node left silent is not re-alerted. Every further timeout period it
   would have been alerted again counts as a suppressed duplicate.
5. When the device sends again (simulator running, or `r` after `d`), it is
   removed from the set and a green **RECOVERED** event appears.

### 4. Undo (Stack)

1. Click **Open valve** on *Valve 1* (Valve 2 stays closed).
2. Click **Close all valves**. Both valves are now closed, and the command
   history shows the stack with the newest entry on top.
3. Click **↶ Undo**. *Valve 1* reopens and *Valve 2* stays closed. Undo
   restored exactly the states saved before the command ran, it did not
   just "open everything".
4. Keep clicking **↶ Undo** to walk back through every command. When the
   stack is empty, Undo shows *"Nothing to undo - the command history is
   empty."* (the API answers 409) and nothing crashes.

### 5. Search, filters and suggestions

1. Type `Moisture Sensor B` in the search box and press Enter. The table
   filters, and the **Suggested Actions** panel offers **🔍 Pump A** with a
   reason like *"After searching Moisture Sensor B, operators searched for
   Pump A within a minute (5 of 6 times)."*
2. Search `Power Meter A` and the panel offers **▶ Toggle Relay 1**. Click
   it: the command runs in one click and lands on the undo stack.
3. With the simulator running, moisture sensors slowly dry out. When *Moisture
   Sensor B* drops below 20%:
   - it appears under **Automated insights** (out of range);
   - before that, a steady fall shows as a *trend* warning;
   - the panel offers **▶ Trigger Pump A** because of the current condition,
     with *"When Moisture Sensor B drops below 20%, operators ran 'Trigger
     Pump A' within a minute (6 of 7 times)."*

   While any pump is on, the simulator's soil moisture rises again.

### 6. Navigation keeps state

1. Set a search and a category filter, then select a device.
2. Go to **Sensor Telemetry**: register a sensor, upload an attachment,
   watch the Mesh Pulse.
3. Come back to **Command Stream & History**. The filters, the selected
   device's timeline, the feed, the alerts and the command history are
   exactly as you left them.

## The Automated Action Engine

**How it predicts.** `SuggestionEngine` keeps a bounded queue (500 entries)
of timestamped events: every search, every manual command, and every device
condition such as *"Moisture Sensor B drops below 20%"*. When the operator
acts within 5 minutes of their previous action, the pair is counted in a
transition table, a `Dictionary<string, Dictionary<string, int>>` that reads
"after X, the user did Y, N times", together with the total time gap.
Conditions are counted the same way against the next operator action, so
the engine also learns "after this condition, the operator usually does Y".

To suggest, it takes the row for the latest action, or for an active
condition, and ranks the followers by count. A follower must have been seen
at least twice. The confidence is count / row total, and the explanation
comes from the count and the average gap. When an old event falls out of
the bounded queue, the transitions it created are subtracted, so the model
always reflects only the remembered window.

**How it spots problem devices.** Separately, `DeviceHealthAnalyzer` checks
every device's live state and the last 6 readings from its sorted history.
It flags, in this order:
- faults (an active critical alert);
- disconnections;
- out-of-range readings (past a warning threshold);
- sustained trends: six readings moving the same way with a large total
  change, which is an early warning before a threshold is crossed.

The engine is seeded with about three hours of simulated operator history
(`SmartX.Api/Data/InsightsSeed.cs`), so the suggestions are visible on first
launch.

## Thread safety

The worker, the disconnect monitor, the insights publisher and the HTTP
endpoints all touch the same singleton structures at the same time. Each
structure therefore guards itself with a private `System.Threading.Lock`:

- `PacketQueue<T>`, `CriticalAlertQueue<T>`, `DeviceDirectory`,
  `ReadingHistoryStore<T>`, `ActiveAlertSet` and `DisconnectDetector` lock
  every member.
- Each critical section is O(1) or O(log n), so contention is negligible.
- `DeviceDirectory` stores immutable `LiveDevice` records and replaces them
  rather than mutating them, so a reader never sees a half-updated device.
- `IngestionPipeline` takes one extra lock around "submit a batch" and "take
  next", so a simulated batch lands atomically relative to the worker.
- `OverrideCommandProcessor` uses one lock for the actuator board **and** the
  command stack together. An execute and an undo from two browser tabs can
  never interleave halfway.
- `SuggestionEngine` is not thread-safe on its own. `InsightsService` wraps
  every call in a lock.
- Counters use `Interlocked`, and the Part 1 `SensorLedger` now locks too
  (see `FEEDBACK_UPDATES.md`).

## Where each requirement lives

### Part 1

| Requirement | Location |
|---|---|
| Generic `TelemetryPacket<T>` | `SmartX.Shared/Models/TelemetryPacket.cs` (reused by Part 2 ingest in `SmartX.Shared/Realtime/TelemetryIngestMapper.cs`) |
| Operator overloading | `SmartX.Shared/Models/PowerReading.cs`, `MoistureReading.cs` |
| Jagged/multi-dimensional arrays → `List<T>` | `SmartX.Shared/Processing/TelemetryBatchProcessor.cs`, seeded in `SmartX.Api/Services/TelemetryHistoryStore.cs` |
| Recursive deployment validator | `SmartX.Shared/Validation/DeploymentTreeValidator.cs` |
| Custom collection | `SmartX.Shared/Collections/SensorLedger.cs` |
| Dynamic engagement feature | `SmartX.Api/Services/TelemetryPulseGenerator.cs` + `SmartX.Client/Components/MeshPulseWidget.razor` |
| File upload | `SmartX.Api/Services/AttachmentStorage.cs`, `SmartX.Client/Components/SensorRegistry.razor` |

### Part 2

| Requirement | Location |
|---|---|
| Menu with two active pillars + disabled third | `SmartX.Client/Layout/NavMenu.razor`, `SmartX.Client/Pages/Home.razor`, `SmartX.Client/Pages/ComingSoon.razor` |
| Singleton client state container (no lost state on navigation) | `SmartX.Client/Services/LiveStateContainer.cs`, registered in `SmartX.Client/Program.cs` |
| SignalR hub + live push | `SmartX.Api/Hubs/TelemetryHub.cs`, `SmartX.Shared/Realtime/HubEvents.cs` |
| Connection state + graceful fallback (polling) | `SmartX.Client/Services/TelemetryHubConnection.cs`, `SmartX.Client/Components/ConnectionBadge.razor` |
| `Queue<T>` FIFO for telemetry | `SmartX.Shared/Realtime/PacketQueue.cs` |
| Ingest endpoint returning 202 | `SmartX.Api/Endpoints/TelemetryEndpoints.cs` (`POST /api/telemetry/ingest`) |
| `PriorityQueue<TElement,TPriority>` with severity + insertion-counter tiebreaker | `SmartX.Shared/Realtime/CriticalAlertQueue.cs`, `SmartX.Shared/Realtime/AlertPriority.cs` |
| Priority drained before FIFO | `SmartX.Shared/Realtime/IngestionPipeline.cs` |
| `BackgroundService` worker | `SmartX.Api/Services/TelemetryProcessingWorker.cs` |
| `Stack<T>` command history + Undo (inverse, empty-stack safe) | `SmartX.Shared/Commands/OverrideCommandStack.cs`, `OverrideCommandProcessor.cs`, `ActuatorStateBoard.cs`, `SmartX.Api/Endpoints/CommandEndpoints.cs` |
| `Dictionary<TKey,TValue>` devices keyed by MAC | `SmartX.Shared/Realtime/DeviceDirectory.cs` |
| `SortedDictionary<DateTime,T>` capped history | `SmartX.Shared/Realtime/ReadingHistoryStore.cs` |
| `HashSet<T>` active alerts / disconnected nodes + recovered event | `SmartX.Shared/Realtime/ActiveAlertSet.cs`, `SmartX.Api/Services/AlertBroadcaster.cs` |
| Disconnect detector (configurable timeout) | `SmartX.Shared/Realtime/DisconnectDetector.cs`, `SmartX.Api/Services/DisconnectMonitor.cs`, `SmartX.Api/appsettings.json` |
| Command Stream dashboard (live feed, commands, undo) | `SmartX.Client/Pages/CommandStream.razor`, `Components/LiveFeed.razor`, `Components/OverridePanel.razor`, `Components/AlertPanel.razor`, `Components/QueueStatsBar.razor` |
| Search & filter (category, alert state, MAC, name) | `SmartX.Client/Components/DeviceFilterBar.razor`, `Components/DeviceTable.razor`, `SmartX.Client/Services/DeviceFilter.cs` |
| Timeline chart per device | `SmartX.Client/Components/DeviceTimeline.razor` (inline SVG), `GET /api/stream/history/{mac}` |
| Predictive Action Engine (transition model) | `SmartX.Shared/Insights/SuggestionEngine.cs` |
| Fault / out-of-range / trend detection | `SmartX.Shared/Insights/DeviceHealthAnalyzer.cs` |
| Engine wiring, real-time push, seeded history | `SmartX.Api/Services/InsightsService.cs`, `InsightsPublisher.cs`, `SmartX.Api/Data/InsightsSeed.cs`, `SmartX.Api/Endpoints/InsightsEndpoints.cs` |
| "Suggested Actions" panel | `SmartX.Client/Components/SuggestedActionsPanel.razor` |
| Simulated devices over HTTP (float/int/bool, flags, keys) | `SmartX.Simulator/` |
| "Simulate critical alert" control | Dashboard button in `QueueStatsBar.razor`, `POST /api/simulate/critical-alert` in `SmartX.Api/Endpoints/SimulationEndpoints.cs` |
| Docker (optional simulator service) | `docker-compose.yml`, `SmartX.Simulator/Dockerfile` |
| Unit tests | `SmartX.Tests/` (see below) |

### Tests

| Area | Test file |
|---|---|
| FIFO ordering | `PacketQueueTests.cs` |
| Priority bypass ordering | `IngestionPipelineTests.cs` |
| Severity + tiebreaker ordering | `CriticalAlertQueueTests.cs` |
| Stack undo (empty stack, close-all-valves inverse, multi-step) | `OverrideCommandProcessorTests.cs` |
| Dictionary lookup (hit and miss) | `DeviceDirectoryTests.cs` |
| Sorted history ordering and the cap | `ReadingHistoryStoreTests.cs` |
| HashSet de-duplication and recovery | `ActiveAlertSetTests.cs` |
| Disconnect detector | `DisconnectDetectorTests.cs` |
| Suggestion engine (known history → expected next action) + health analyzer | `SuggestionEngineTests.cs` |
| Search/filter rules, state container | `DeviceFilterTests.cs`, `LiveStateContainerTests.cs` |
| Part 1 (validator, operators, batch processor, ledger, download URL) | `DeploymentTreeValidatorTests.cs`, `OperatorOverloadTests.cs`, `TelemetryBatchProcessorTests.cs`, `SensorLedgerTests.cs`, `SmartXApiClientTests.cs` |

## API reference

`SmartX.Api/SmartX.Api.http` has a ready-to-run request for every route.
In Development mode the OpenAPI document is at
`http://localhost:5074/openapi/v1.json`.

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/deployment-tree` | Facility/zone/sub-zone/node hierarchy |
| GET | `/api/sensors` | List registered sensors |
| GET | `/api/sensors/{mac}` | One sensor |
| POST | `/api/sensors` | Register a sensor (recursive path validation) |
| POST | `/api/sensors/{mac}/attachments` | Upload a config/photo/log |
| GET | `/api/sensors/{mac}/attachments/{id}/download` | Download an attachment |
| GET | `/api/telemetry/pulses` | Mesh Pulse feed |
| GET | `/api/telemetry/history/{sensorId}` | Part 1 flattened jagged-batch history |
| POST | `/api/telemetry/ingest` | **Part 2** - queue one packet, returns 202 |
| GET | `/api/stream/snapshot` | Devices, recent readings, alerts, queue stats |
| GET | `/api/stream/alerts` | The active alert set |
| GET | `/api/stream/history/{mac}` | Sorted per-device timeline |
| GET | `/api/commands` | Actuator states + command stack |
| GET | `/api/commands/actuators` | Commanded actuator states (read by the simulator) |
| POST | `/api/commands` | Execute an override |
| POST | `/api/commands/undo` | Undo the last override (409 when empty) |
| GET | `/api/insights` | Suggested actions + automated insights |
| POST | `/api/insights/search` | Record a dashboard search |
| POST | `/api/simulate/critical-alert` | Backlog + critical packet demo |
| POST | `/api/simulate/disconnect/{mac}` | Mark a node disconnected (repeat for the duplicate demo) |
| WS | `/hubs/telemetry` | SignalR hub: `telemetry`, `alert`, `command`, `queueStats`, `insights` |

## Notes

- All gateway state is in memory and resets when the API restarts.
- Uploaded files go to `SmartX.Api/UploadedFiles/`, which is git-ignored.
- `FEEDBACK_UPDATES.md` lists every change made to Part 1 while building
  Part 2.
