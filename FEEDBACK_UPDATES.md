# Feedback Updates

This file lists the changes made to earlier parts of the Smart-X POE, either
because of lecturer feedback or because a problem turned up while the next
part was being built. Each entry says what was wrong, what changed, and which
files were touched.

## Part 1 changes made during Part 2

| # | What was wrong | What changed | File(s) |
|---|---|---|---|
| 1 | `SensorLedger` was a plain `List<T>` with no locking. That was fine while only the Blazor form registered sensors. In Part 2 the simulator registers devices while the background worker and the endpoints read the ledger, and that can throw "collection was modified" errors. | Every member now takes a private lock, and enumeration walks a snapshot copy. The existing tests still pass unchanged. | `SmartX.Shared/Collections/SensorLedger.cs` |
| 2 | Sensors could only be identified by MAC address, so the Part 2 requirement to search by name had nothing to search. | Added an optional `DisplayName` (and an `ActuatorKind` for actuators) to the registration request and the sensor record. The form has two new fields, the registry shows a Name column, and the seed data uses friendly names. | `SmartX.Shared/Models/SensorRecord.cs`, `SmartX.Shared/Dtos/SensorRegistrationRequest.cs`, `SmartX.Api/Data/SensorSeed.cs`, `SmartX.Client/Components/SensorRegistrationForm.razor`, `SmartX.Client/Components/SensorRegistry.razor` |
| 3 | The Mesh Pulse widget ran its own random walk. It could show impossible values such as 108% moisture, it showed actuators as "1.6state", and it ignored real telemetry. | The widget now uses a device's real reading whenever the device is reporting. Moisture is capped at 100%, actuators show ON/OFF and are never scored as anomalies, and disconnected nodes show the grey "Disconnected" state. | `SmartX.Api/Services/TelemetryPulseGenerator.cs`, `SmartX.Client/Components/MeshPulseWidget.razor` |
| 4 | Leaving the Telemetry page and coming back reset it to the first tab, and the Mesh Pulse showed "waiting for the first pulse" again. | The open tab and the last pulse snapshot now live in the singleton `LiveStateContainer`, so the page comes back exactly as it was left. | `SmartX.Client/Pages/Telemetry.razor`, `SmartX.Client/Components/MeshPulseWidget.razor`, `SmartX.Client/Services/LiveStateContainer.cs` |
| 5 | The Command Stream menu item and Home pillar were locked "coming soon" placeholders. | Both are now live and open the Part 2 dashboard. The coming-soon page only covers Mesh Routing (final PoE). | `SmartX.Client/Layout/NavMenu.razor`, `SmartX.Client/Pages/Home.razor`, `SmartX.Client/Pages/ComingSoon.razor` |
| 6 | `HttpClient` and the API client were registered as scoped, which a singleton state container and SignalR connection cannot depend on. | Both are now singletons. In Blazor WebAssembly there is one user per app instance, so this changes nothing for Part 1 pages. The SignalR hub URL is built from the same configured base address, so no second hardcoded port was added. | `SmartX.Client/Program.cs`, `SmartX.Client/Services/TelemetryHubConnection.cs` |

## Lecturer feedback on Part 1

No written feedback had been received when Part 2 was built. Entries will be
added here when it arrives.

## Lecturer feedback on Part 2

Part 2 was marked 90%. The only written comment was:

> "Some external sources are cited in source comments"

What was done in response, in Part 3:

| # | Response | Where |
|---|---|---|
| 1 | Kept citing every external algorithm or reference source in the code comments, next to the code that relies on it. The AVL tree cites Adelson-Velsky and Landis (1962). BFS cites Cormen et al., *Introduction to Algorithms* (3rd ed.), section 22.2. Prim's algorithm cites the same book, section 23.2, and the Microsoft Learn page for `PriorityQueue<TElement,TPriority>`. Where the code is written from general knowledge of an algorithm rather than copied from a source, the comment says so plainly instead of naming a source that was not used. | `SmartX.Shared/Trees/AvlTree.cs`, `SmartX.Shared/Graphs/MeshReachability.cs`, `SmartX.Shared/Graphs/PrimMinimumSpanningTree.cs` |
| 2 | Listed the same sources together in the README's implementation report, so they can be checked in one place. | `README.md` ("Sources" under "Implementation report") |
| 3 | Kept the code attribution document up to date with the Part 3 sources. | `PROG7312_Code_Attribution.docx` |

## Part 2 changes made during Part 3

| # | What was wrong or missing | What changed | File(s) |
|---|---|---|---|
| 1 | The disconnect demo only went one way. A seeded device marked disconnected with **Simulate disconnect** stayed down forever unless the console simulator was running, so the Part 3 "node recovers and routes heal back" step could not be shown from the dashboard alone. | Added `DisconnectDetector.ForceReconnect`, the opposite of `ForceDisconnect`. It clears the alert and stops watching the node until it really reports again. Without the second part, a seeded device with no simulator behind it would be flagged again one timeout later. Added `POST /api/simulate/reconnect/{mac}`, which raises the normal "recovered" event, and a **Simulate reconnect** button on the topology page. Covered by a new test. | `SmartX.Shared/Realtime/DisconnectDetector.cs`, `SmartX.Api/Endpoints/SimulationEndpoints.cs`, `SmartX.Tests/DisconnectDetectorTests.cs` |
| 2 | Disconnects and recoveries only produced an alert, so nothing else on the dashboard could react to the shape of the mesh changing. | `AlertBroadcaster`, which every disconnect and recovery already goes through, now also pushes a freshly computed topology (BFS + MST) over a new `topology` SignalR event. Duplicate disconnects push nothing, because nothing changed. | `SmartX.Api/Services/AlertBroadcaster.cs`, `SmartX.Api/Services/TopologyBroadcaster.cs`, `SmartX.Shared/Realtime/HubEvents.cs` |
| 3 | The worker only stored history in the capped `SortedDictionary` (200 points per device), which is right for the timeline chart but too short for time-range questions. | The same worker step now also inserts each reading into the device's AVL tree. `ReadingHistoryStore` and the Part 2 timeline are unchanged. | `SmartX.Api/Services/TelemetryProcessingWorker.cs`, `SmartX.Api/Program.cs` |
| 4 | The client resync and polling fallback did not know about the topology. | The hub connection listens for `topology`, and every resync (reconnect or offline polling) also fetches `/api/topology`. The state container ignores a snapshot older than the one it holds, so a REST response and a push that cross on the wire cannot roll the map back. Covered by new tests. | `SmartX.Client/Services/TelemetryHubConnection.cs`, `SmartX.Client/Services/LiveStateContainer.cs`, `SmartX.Tests/LiveStateContainerTests.cs` |
| 5 | The Mesh Routing menu item and Home pillar were locked "coming soon" placeholders. | Both now open the Network Topology page. The coming-soon page was removed because nothing points to it any more. | `SmartX.Client/Layout/NavMenu.razor`, `SmartX.Client/Pages/Home.razor`, `SmartX.Client/Pages/ComingSoon.razor` (removed) |
| 6 | A device registered by the simulator only appeared on the dashboard tables. | The registration endpoint pushes a fresh topology, so a placed device joins the mesh map live. | `SmartX.Api/Endpoints/SensorEndpoints.cs` |
