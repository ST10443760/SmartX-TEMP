# Smart-X IoT Mesh Gateway - Final PoE (Parts 1, 2 & 3)

📺 Part 1 demo video: https://youtu.be/y5HmTqGUoi8

📺 Part 2 demo video: https://youtu.be/ixgkVTnZp1Q

📺 Part 3 demo video: https://youtu.be/QKaKJ5zSwYA

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
- **Part 3 - Network Topology & Mesh Routing.** Models the mesh as a graph and
  each sensor's history as a tree:
  - a hand-written **AVL tree** per device answers time-range history queries
    without scanning everything;
  - the mesh is a weighted, undirected **graph** (adjacency list) whose link
    weight is the power cost of a transmission;
  - **BFS** from the gateway finds which nodes can still get packets home,
    which are down and which are cut off, and where traffic reroutes;
  - **Prim's algorithm** (with a `PriorityQueue` min-heap) builds the minimum
    spanning tree, and each node's path in it is its power-efficient route;
  - when a node disconnects or recovers, routes and the MST recompute and the
    topology page updates live over SignalR (self-healing).

All three pillars are reachable from one menu, and the state of every page
lives in one singleton client container, so moving between them loses nothing.

## Architecture

```
SmartX.slnx
  SmartX.Shared/     Class library shared by every project.
    Models/, Dtos/     Types that cross the wire (TelemetryPacket<T>, LiveReading, TopologySnapshot, ...)
    Collections/       Part 1 SensorLedger (List<T>-backed custom collection)
    Validation/        Part 1 recursive deployment-tree validator
    Processing/        Part 1 jagged array -> List<T> batch processor
    Realtime/          Part 2 queues, device dictionary, sorted history, alert set, disconnect detector
    Commands/          Part 2 override command stack, actuator board, command processor (undo)
    Insights/          Part 2 suggestion engine + device health analyzer
    Trees/             Part 3 AVL tree, range query result, per-device history trees
    Graphs/            Part 3 mesh graph, BFS reachability, Prim's MST, route lookup
  SmartX.Api/        ASP.NET Core Minimal API + SignalR hub + background workers
  SmartX.Client/     Blazor WebAssembly dashboard (talks to the API over HTTP + SignalR only)
  SmartX.Simulator/  Console app: a fake device fleet that registers and POSTs telemetry over HTTP
  SmartX.Tests/      xUnit tests for every data structure and algorithm
```

- **Backend:** ASP.NET Core Minimal API on .NET 10, SignalR for live push.
- **Frontend:** Blazor WebAssembly (standalone), .NET 10. All charts and the
  mesh map are inline SVG, with no chart or graph library.
- **State:** in memory, which is enough for the simulation brief. It resets
  when the API restarts.

### How a packet flows

```
Simulator / device ──POST /api/telemetry/ingest──► dictionary lookup (unknown MAC → 404)
                                                    │
                     Warning/Critical ──► CriticalAlertQueue (PriorityQueue, severity + counter)
                     Normal ───────────► PacketQueue (Queue<T>, FIFO)
                                                    │  202 Accepted returned immediately
TelemetryProcessingWorker (BackgroundService) ◄─────┘  drains priority queue FIRST, then FIFO
   ├─ DeviceDirectory (Dictionary by MAC) ── latest value
   ├─ ReadingHistoryStore (SortedDictionary<DateTime,…> per device, capped) ── Part 2 timeline chart
   ├─ DeviceHistoryTrees (one AVL tree per device, capped) ── Part 3 time-range history queries
   ├─ ActiveAlertSet (HashSet) ── raise once / suppress duplicate / recover
   ├─ InsightsService ── records device conditions for the action engine
   └─ SignalR hub ──► every open dashboard (telemetry, alerts, commands, queue stats, insights)
```

### How the mesh view is computed (Part 3)

```
ActiveAlertSet (Part 2) ── which nodes are disconnected (single source of truth)
DeviceDirectory ───────── which devices are registered
MeshTopologySeed ──────── node positions + which pairs of nodes can hear each other
        │
        ▼  MeshTopologyService (on every request / push, nothing cached)
   MeshGraph (adjacency list) ──► MeshReachability (BFS)  ──► reachable / down / cut off / reroutes
                              └─► PrimMinimumSpanningTree ──► MST + total cost ──► MeshRouter (route per node)
        │
        ▼
GET /api/topology, /reachability, /mst, /route/{device}
AlertBroadcaster: node disconnected or recovered ──► TopologyBroadcaster ──► SignalR "topology" event
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

`dotnet test` runs 154 xUnit tests: 90 from Parts 1 and 2, and 64 for Part 3, including the fixes made after the first build.

## Running locally (no Docker) - two or three terminals

You need **two terminals** for the app itself (API + client) and **a third,
optional one** for the device simulator. Without the simulator, everything
still works:
- the seeded devices just don't stream live telemetry;
- the **Simulate critical alert**, **Simulate disconnect** and **Simulate
  reconnect** buttons still exercise the queues, the alert set and the mesh
  rerouting;
- every device already has four hours of seeded history for the Part 3 range
  queries.

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
the API seeds at startup. The 6 simulator devices (`5C:CF:7F:00:00:01` to
`06`) have places in the mesh topology, so they appear on the Network
Topology map as soon as they register. Each device type sends its native
value type:

| Device | Wire value | Example |
|---|---|---|
| Moisture sensor | `float` | `{ "macAddress": "...", "moisture": 44.5 }` |
| Power meter | `int` | `{ "macAddress": "...", "watts": 612 }` |
| Pump / valve / relay | `bool` | `{ "macAddress": "...", "valveOpen": true }` |

Keys while the simulator runs:

| Key | Effect |
|---|---|
| `c` | Send a critical spike (moisture 6.5% or ~4300 W) for a random sensor on the next round |
| `d` | Silence a random sensor (it stops sending - the gateway flags it once after the timeout, and the mesh reroutes) |
| `r` | Reconnect every silenced device (the gateway raises "recovered" events, and the mesh heals back) |
| `q` | Quit |

Flags (also available as `SIM_*` environment variables): `--api <url>`,
`--devices <n>`, `--interval <seconds>`, `--no-seeded`, `--spike`,
`--spike-every <seconds>`, `--disconnect <mac|name>` (silences that device
after round 3). Run `dotnet run --project SmartX.Simulator -- --help` for
the full list.

> Both client dev-server ports (5219, 7036) and the Docker client port
> (8081) are in the API's CORS policy (`SmartX.Api/Program.cs`). If you
> change a port, update `WithOrigins(...)` and `ApiBaseUrl`.

## Running with Docker - one terminal

```bash
docker compose up --build                      # API + client
docker compose --profile simulator up --build  # API + client + simulator
```

- API: `http://localhost:8080`
- Client: `http://localhost:8081`

One terminal is enough: compose starts every container. The client is
published as static files served by nginx, and its
`appsettings.Production.json` points at `http://localhost:8080/`.
Attachment download links and the SignalR hub URL both come from that one
setting.

The simulator service is optional (compose profile `simulator`). Inside the
compose network it reaches the API at `http://smartx-api:8080/`. Because a
container has no keyboard, it sends a critical spike every 45 seconds and
silences *Moisture Sensor D* after round 3, which also shows up as a down node
on the Network Topology map. Both settings are in `docker-compose.yml`. To use
the keys, run `docker attach smartx-smartx-simulator-1` and detach with
`Ctrl+P, Ctrl+Q`.

## Using the app

1. **Home**: three active pillars (Telemetry, Command Stream, Network
   Topology). The top bar shows the live SignalR connection state on every
   page:
   - **Live**: connected;
   - **Reconnecting...**: the socket dropped and is retrying;
   - **Offline - polling**: the dashboard falls back to fetching
     `/api/stream/snapshot` and `/api/topology` every 3 seconds until the
     socket comes back.
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
4. **Network Topology & Mesh Routing (Part 3)**:
   - **Mesh network map:** every placed node at its site-plan position.
     - The gateway is the dark **GW** square.
     - A node's colour shows its latest live reading: green healthy, pale
       green healthy but no live reading yet, amber warning, red critical.
     - A disconnected node is grey with a red ring and a cross, and its links
       turn dashed red.
     - A node that is up but cut off is drawn as a dashed orange ring.
     - MST links are drawn heavier, and the selected route is the thick
       indigo path.
     - Hover a node or a link for details.
   - **Search:** the same rules as the Command Stream search (MAC or name,
     case-insensitive, filters as you type). Press Enter to pick the first
     match, or click a match. Clicking a node on the map also selects it.
   - **Route to the gateway:** the selected node's hops in order, each hop's
     power cost and the total, with **Simulate disconnect** and **Simulate
     reconnect** buttons.
   - **Reachability (BFS):** reachable count, down and cut-off nodes, and a
     *Self-healing* box showing where each down node's traffic hops instead.
     It also shows the BFS discovery order.
   - **Minimum spanning tree (Prim's):** total power cost, number of links,
     number of trees in the forest, the excluded (down) nodes, any live but
     unreachable nodes, and the MST links in the order Prim's added them.
     There is a toggle for the MST highlight.
   - **Historical log:** From/To range pickers (browser local time), quick
     presets, a time-sorted table paged 25 at a time, and the readout
     *"N readings in range · AVL range query visited V of T stored readings"*.

   Filters, the feed, the selected device, the alert list, the command
   history, the topology search, the selected node, the MST toggle and the
   history range all live in a singleton `LiveStateContainer`. Moving to
   another page and back keeps everything.

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

## Testing the Part 3 structures

The steps below assume the API and client are running (Terminals 1 and 2;
the simulator is not needed) and the **Network Topology & Mesh Routing**
page is open (menu item 4, or the third pillar on Home). Every step also has
a ready-made request in `SmartX.Api/SmartX.Api.http`.

### 1. Run a historical range query (AVL tree)

1. Click **Moisture Sensor C** on the map (top right), or type `sensor c` in
   the search box and press Enter.
2. The **Historical log** card under the map runs a query straight away for
   the last hour. The readout shows something like *"710 readings in range ·
   AVL range query visited 717 of 2,880 stored readings (24.9%)"*, and the
   table lists the readings oldest first, 25 per page.
3. Click **Last 15 min**. You get about 180 readings while visiting under 190
   tree nodes, roughly 6.5% of the tree. A narrow range touches a small part
   of the tree; a wide range has to visit every matching reading.
4. Pick your own range: set **From** and **To** (for example 14:00 and 16:00
   today, as long as it falls inside the *"History held for ..."* line shown
   under the readout) and click **Run query**.
5. Use **Next →** and **← Previous** to page through the results. The page
   count is the number of matches divided by 25.
6. API alternative:
   `curl "http://localhost:5074/api/history/24:6F:28:A1:00:03?from=2026-11-12T14:00:00Z&to=2026-11-12T16:00:00Z&page=1&pageSize=50"`
   (the times are UTC). The response has `matchCount`, `nodesVisited` and
   `totalReadings`.

### 2. View a route for a node (MST path)

1. With **Moisture Sensor C** selected, the **Route to the gateway** card
   shows:

   | # | From | | To | Power cost |
   |---|---|---|---|---|
   | 1 | Moisture Sensor C | → | Pump A | 2.8 |
   | 2 | Pump A | → | Gateway | 3.7 |
   | | **Total (2 hops)** | | | **6.5** |

2. On the map the same two links are drawn as a thick indigo path, and the
   nodes on it get a ring.
3. Click any other node (for example **Valve 1**) to see its route: Valve 1 →
   Power Meter A → Power Meter B → Gateway, total 14.8. Valve 1 also has a
   direct 33 m link to the gateway (cost 11.1), but the MST does not use it.
   Hanging Valve 1 off Power Meter A adds only 2.7 to the network total, while
   the direct link would add 11.1. This shows the MST minimising the cost of
   the whole network rather than each node's own route (see the Prim's
   section of the implementation report).
4. API alternative: `curl "http://localhost:5074/api/topology/route/Moisture%20Sensor%20C"`

### 3. Disconnect a node and watch the network heal (BFS + MST recompute)

1. Keep **Moisture Sensor C** selected and click **Pump A** on the map. Then
   click **Simulate disconnect** in the route card. (The **Simulate
   disconnect** button on the Command Stream timeline does exactly the same,
   and the topology page updates either way.)
2. Click **Moisture Sensor C** again. Without a refresh:
   - Pump A turns grey with a red cross, and its links turn dashed red;
   - Moisture Sensor C's route becomes C → Moisture Sensor D → Relay 1 →
     Valve 2 → Gateway, total **19.6**;
   - the **Reachability** card shows *9 of 10 reachable*, lists Pump A as
     **DOWN**, and the *Self-healing* box shows where Pump A's neighbours now
     send traffic (via Power Meter B, via Moisture Sensor B, via Moisture
     Sensor C → D → Valve 2);
   - the **MST** card shows **38.8** total, 9 links, "Left out (down): Pump A".
3. Now select **Moisture Sensor D** and click **Simulate disconnect** too.
   Moisture Sensor C is still up, but both of its links lead to down nodes:
   - it turns into a dashed orange **Cut off** ring;
   - its route card says *"is up, but every path to the gateway runs through
     a disconnected node"*;
   - BFS lists it as **CUT OFF**;
   - the MST card shows **2 trees in the forest** and lists it as "Up but
     unreachable from the gateway".
4. Click **Simulate reconnect** on Moisture Sensor D, then on Pump A. Each
   one heals the mesh live, and C's route goes back to 6.5 with an MST of
   37.2.
5. **With the simulator running**, a button disconnect only lasts until the
   device's next packet, about 2 seconds, because a real packet means the node
   is back. So the map heals almost at once. To see the reroute with the
   simulator running:
   - silence the node from the simulator instead: start it with
     `--disconnect "Pump A"`, or press `d`;
   - the disconnect detector flags the silent node after 10 seconds and the
     map reroutes on its own;
   - `r` brings it back.

   With the 6 simulator devices on the map, the MST is different, so the
   numbers differ from the ones above. For example, with Pump A silenced,
   Moisture Sensor C's MST route becomes 12 hops long, a good illustration
   that an MST path is not a shortest path.
6. API alternative: `curl -X POST http://localhost:5074/api/simulate/disconnect/7C:9E:BD:33:22:06`
   then `curl -X POST http://localhost:5074/api/simulate/reconnect/7C:9E:BD:33:22:06`.

### 4. View the MST (Prim's)

1. The **Minimum spanning tree (Prim's)** card shows the total power cost
   (**37.2** with all 10 seeded devices up) and the number of links (10, one
   fewer than the 11 vertices including the gateway).
2. Open **MST links in the order Prim's added them**: the list starts at the
   gateway with its cheapest link (Gateway → Pump A, 3.7), then always takes
   the cheapest link leaving the tree.
3. On the map the MST links are the heavier lavender lines. Untick **Highlight
   MST links on the map** to compare with the full mesh.
4. With the simulator running, its six devices join the graph as they
   register, and the MST grows to include them.
5. API alternative: `curl http://localhost:5074/api/topology/mst`

### 5. Navigation keeps state

1. Select a node, set a history range and page forward.
2. Go to **Command Stream & History** and back. The same node, route, search
   text, history range, page and MST toggle are still there.

## Implementation report (Part 3 data structures)

### AVL tree - historical telemetry queries

**What it is.** A binary search tree that balances itself. Every node keeps
its height. After an insert (or a removal), the nodes on the path back to the
root are re-measured, and any node whose left and right heights differ by 2
is fixed with a rotation:
- **left-left:** a single right rotation;
- **right-right:** a single left rotation;
- **left-right:** rotate the left child left, then the node right;
- **right-left:** rotate the right child right, then the node left.

This keeps the height under about 1.44·log₂(n).

**Why the Network Topology feature needs it.** Each device produces a reading
every few seconds, and the topology page asks "show me this sensor between
14:00 and 16:00". A list would have to be scanned end to end. A plain binary
search tree fed with timestamps in order (which is how telemetry arrives)
degenerates into a linked list of height n. The AVL tree stays balanced, so
it can jump to the start of the range and walk only the part that matches.
The range query also prunes:
- it goes left only if the current key is after `from`;
- it goes right only if the current key is before `to`.

Whole subtrees outside the range are never entered, and the page shows how
many nodes were visited next to how many readings exist.

**Complexity.** Insert O(log n); remove-oldest (used for the memory cap)
O(log n); range query O(log n + k) nodes for k results; in-order walk O(n).
Memory O(n). The cap is 10,000 readings per device (about 5.5 hours at one
reading every 2 seconds, about 32 MB for 16 devices). Readings with the same
timestamp share one node and are all returned.

**Code (`SmartX.Shared/Trees/AvlTree.cs`):**

```csharp
private static void RangeQuery(AvlNode? node, DateTime from, DateTime to, List<KeyValuePair<DateTime, TValue>> items, ref int visited)
{
    if (node is null) return;
    visited++;

    if (from < node.Key)                       // only the left subtree can hold smaller keys
        RangeQuery(node.Left, from, to, items, ref visited);

    if (from <= node.Key && node.Key <= to)    // inclusive at both ends
        foreach (var value in node.Values)
            items.Add(new KeyValuePair<DateTime, TValue>(node.Key, value));

    if (node.Key < to)                         // only the right subtree can hold larger keys
        RangeQuery(node.Right, from, to, items, ref visited);
}
```

### Graph - the mesh network model

**What it is.** An undirected, weighted graph stored as an adjacency list:
`Dictionary<string, List<MeshEdge>>` from each vertex (a node's MAC, or
`GATEWAY`) to the links leaving it. Every link is stored once in each
direction with the same weight.

**Why the Network Topology feature needs it.** A mesh *is* a graph: devices
are vertices, and wireless links are edges between devices that are within
radio range of each other. A node has only a few neighbours, so an adjacency
list uses O(V + E) memory instead of a matrix's O(V²). Listing a node's
neighbours is O(degree), and that is the operation BFS and Prim's do
constantly.

**Link weight (power cost).** In free space, signal strength falls off with
roughly the square of distance, so the power needed to cross a link grows
with distance². Each node has a position in metres in
`SmartX.Api/Data/MeshTopologySeed.cs`, and the weight is distance² / 100:
- a 10 m hop costs 1.0;
- a 20 m hop costs 4.0;
- a 33 m hop costs about 11.

That is why the MST prefers chains of short relay hops over one long hop.

**Complexity.** Add vertex O(1); add edge O(degree) (it checks for a
duplicate first); neighbours O(1) to get the list; memory O(V + E).

**Code (`SmartX.Shared/Graphs/MeshGraph.cs`):**

```csharp
if (fromA.Exists(e => string.Equals(e.To, b, StringComparison.OrdinalIgnoreCase)))
{
    return false;                         // no parallel links - first weight is kept
}

fromA.Add(new MeshEdge(a, b, weight));    // undirected: one entry in each direction
fromB.Add(new MeshEdge(b, a, weight));
EdgeCount++;
```

### BFS - reachability and rerouting

**What it is.** Breadth-first search from the gateway using a `Queue<T>`:
1. start with the gateway;
2. repeatedly take the oldest discovered node and add each neighbour not
   seen yet;
3. record each node's parent and hop count as it is found.

**Why the Network Topology feature needs it.** It answers "which devices can
still get packets to the gateway right now?". Down nodes (taken from the
Part 2 `ActiveAlertSet`, the single source of truth for disconnections) are
skipped as if removed. A node that is up but only reachable through a down
node is therefore never discovered and is reported as **cut off** rather than
reachable. For each down node, BFS also lists its live neighbours that can
still reach the gateway, with their path. That is where the traffic hops
instead, shown as the *Self-healing* box. Because BFS visits nodes in order of
distance, the parent pointers also give the fewest-hops path.

**Complexity.** O(V + E) time, O(V) memory. Each node is queued at most once.

**Code (`SmartX.Shared/Graphs/MeshReachability.cs`):**

```csharp
while (queue.Count > 0)
{
    var current = queue.Dequeue();
    order.Add(current);

    foreach (var edge in graph.Neighbours(current))
    {
        if (downNodes.Contains(edge.To) || parent.ContainsKey(edge.To))
        {
            continue;                 // down nodes are treated as removed; seen nodes are skipped
        }

        parent[edge.To] = current;
        hops[edge.To] = hops[current] + 1;
        queue.Enqueue(edge.To);
    }
}
```

### Prim's algorithm - minimum spanning tree and power-efficient routes

**What it is.** It grows a tree from the gateway:
1. push every link leaving the tree onto a min-heap
   (`PriorityQueue<MeshEdge, (double Weight, long Order)>`);
2. pop the cheapest link;
3. skip it if both ends are already in the tree (it would make a cycle),
   otherwise add the new node and push its links.

The result is the set of links that connects every reachable node at the
lowest total power cost. This is the "lazy" version of Prim's: stale heap
entries are skipped when popped instead of being decreased in place. The
insertion counter in the priority breaks ties, so the tree is always the same
for the same input.

**Why the Network Topology feature needs it.** Every transmission costs
power, and battery-powered nodes should not waste it. The MST is the cheapest
backbone that still keeps every live node connected to the gateway. A tree
has exactly one path between any two nodes, so each node's path in it is its
route, and `MeshRouter` reads that route off by following parent links.
Prim's runs only over live nodes:
- when a node goes down, the tree is rebuilt around it (the self-healing
  shown on the page);
- if some live nodes cannot reach the gateway, Prim's restarts from the first
  unvisited node, giving a spanning forest instead of crashing or looping;
- those extra trees are listed as unreachable.

The MST minimises the **total** cost of the network, not each node's own
cost to the gateway (that would be a shortest-path tree). The brief defines a
node's route as its MST path, so that is what is shown.

**Complexity.** O(E log E) time, which is O(E log V) for a sparse mesh;
O(V + E) memory. A connected graph's MST has V − 1 edges, and the tests check
this.

**Code (`SmartX.Shared/Graphs/PrimMinimumSpanningTree.cs`):**

```csharp
componentRoot[start] = start;
PushLinks(start);

while (heap.TryDequeue(out var edge, out _))
{
    if (componentRoot.ContainsKey(edge.To))
    {
        continue;                    // both ends already in the tree: would make a cycle
    }

    componentRoot[edge.To] = start;
    edgeToParent[edge.To] = edge;    // parent link = this node's next hop towards the gateway
    edges.Add(edge);
    PushLinks(edge.To);
}
```

### Sources

The algorithms are written from general knowledge, and the code comments say
so. The references named in the comments are:
- G. M. Adelson-Velsky and E. M. Landis (1962), *An algorithm for the
  organization of information* (the original AVL tree paper);
- Cormen, Leiserson, Rivest and Stein, *Introduction to Algorithms* (3rd
  ed.), section 22.2 (breadth-first search) and section 23.2 (Prim's
  algorithm);
- Microsoft Learn, *PriorityQueue<TElement,TPriority> Class*
  (https://learn.microsoft.com/dotnet/api/system.collections.generic.priorityqueue-2).

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
- **Part 3:** `DeviceHistoryTrees<T>` takes one private lock around the
  per-device dictionary and every AVL tree in it. The worker inserts while
  the history endpoint runs range queries, so both happen under the same
  lock. The `AvlTree<T>` itself is deliberately not thread-safe; it is only
  ever touched through the store. Insert is O(log n) and a range query is
  O(log n + k), so the lock is held briefly. A test hammers the store from
  parallel writers and readers.
- **Part 3:** the mesh graph needs no lock. `MeshTopologyService` builds a
  fresh `MeshGraph` for every request from the immutable topology definition
  plus snapshots of the directory and the alert set, runs BFS and Prim's on
  it, and throws it away. No graph is ever shared between threads. With 17
  nodes this takes microseconds.
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
| Menu with active pillars | `SmartX.Client/Layout/NavMenu.razor`, `SmartX.Client/Pages/Home.razor` |
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

### Part 3

Line numbers point at the start of the relevant class, method or block.

| Requirement | Location |
|---|---|
| **A. Menu and integration** | |
| Network Topology menu item active | `SmartX.Client/Layout/NavMenu.razor:37` |
| Home pillar active | `SmartX.Client/Pages/Home.razor:28` |
| Topology selection, search, MST toggle kept in the singleton container | `SmartX.Client/Services/LiveStateContainer.cs:114` |
| History range and page kept in the singleton container | `SmartX.Client/Services/LiveStateContainer.cs:155` |
| **B. Network Topology page** | |
| Page layout | `SmartX.Client/Pages/NetworkTopology.razor:20` |
| Inline SVG mesh (nodes, links, distinct gateway) | `SmartX.Client/Components/MeshMap.razor:29` |
| Node colour from latest reading, plus down / cut off | `SmartX.Client/Components/MeshMap.razor:177` |
| Search by MAC or name (same rule as the Command Stream) | `SmartX.Client/Components/MeshNodeSearch.razor:64` |
| Route hops in order with each cost and the total | `SmartX.Client/Components/RoutePanel.razor:47` |
| Route recomputed when a new topology arrives | `SmartX.Client/Pages/NetworkTopology.razor:110` |
| Historical log: range picker (drafts committed on Run query) | `SmartX.Client/Components/HistoryLog.razor:29`, drafts at `SmartX.Client/Components/HistoryLog.razor:142`, parsing in `SmartX.Client/Services/HistoryRangeInput.cs:17` |
| Historical log: no re-render while a date input has focus | `SmartX.Client/Components/HistoryLog.razor:165` |
| Historical log: presets anchored to the newest stored reading when live data has stopped | `SmartX.Client/Components/HistoryLog.razor:237`, rules in `SmartX.Client/Services/HistoryPresetWindow.cs:15` |
| Historical log: nodes visited vs total readings | `SmartX.Client/Components/HistoryLog.razor:59` |
| Historical log: paging | `SmartX.Client/Components/HistoryLog.razor:93` |
| Live topology over SignalR (client listener) | `SmartX.Client/Services/TelemetryHubConnection.cs:55` |
| Stale snapshot guard (REST and push crossing) | `SmartX.Client/Services/LiveStateContainer.cs:137` |
| Connection state shown as on the Command Stream | `SmartX.Client/Components/ConnectionBadge.razor:6` (top bar of every page) |
| **C. Trees** | |
| Hand-written AVL tree, generic over the value, keyed by timestamp | `SmartX.Shared/Trees/AvlTree.cs:29` |
| Height tracking | `SmartX.Shared/Trees/AvlTree.cs:306` |
| Rebalance after insert, choosing the rotation | `SmartX.Shared/Trees/AvlTree.cs:232` |
| Right rotation / left rotation | `SmartX.Shared/Trees/AvlTree.cs:277` / `SmartX.Shared/Trees/AvlTree.cs:292` |
| Left-right / right-left double rotations | `SmartX.Shared/Trees/AvlTree.cs:247` / `SmartX.Shared/Trees/AvlTree.cs:263` |
| Pruned range query (inclusive) | `SmartX.Shared/Trees/AvlTree.cs:143`, recursion at `SmartX.Shared/Trees/AvlTree.cs:157` |
| Nodes visited by the last query | `SmartX.Shared/Trees/AvlTree.cs:130` |
| Duplicate timestamps (documented behaviour) | `SmartX.Shared/Trees/AvlTree.cs:16` |
| Remove-oldest for the cap (rebalances) | `SmartX.Shared/Trees/AvlTree.cs:96` |
| Source comment | `SmartX.Shared/Trees/AvlTree.cs:24` |
| One tree per device (Dictionary by MAC) | `SmartX.Shared/Trees/DeviceHistoryTrees.cs:24` |
| Memory bound explained | `SmartX.Shared/Trees/DeviceHistoryTrees.cs:11` |
| Private lock | `SmartX.Shared/Trees/DeviceHistoryTrees.cs:27` |
| Fed from the same ingestion path as ReadingHistoryStore | `SmartX.Api/Services/TelemetryProcessingWorker.cs:116` |
| Registered as a singleton with the configurable cap | `SmartX.Api/Program.cs:80` |
| Historical query endpoint (MAC or name, from/to, paging) | `SmartX.Api/Endpoints/HistoryEndpoints.cs:18` |
| Seeded history (4 h, 2,880 readings per device) | `SmartX.Api/Data/HistorySeed.cs:23`, called at `SmartX.Api/Program.cs:107` |
| **D. Graph, traversal and MST** | |
| Graph model: adjacency list, undirected, weighted | `SmartX.Shared/Graphs/MeshGraph.cs:27`, list at `SmartX.Shared/Graphs/MeshGraph.cs:32` |
| Add edge (duplicates, unknown vertices) | `SmartX.Shared/Graphs/MeshGraph.cs:54` |
| Neighbours | `SmartX.Shared/Graphs/MeshGraph.cs:88` |
| Topology definition in one place | `SmartX.Api/Data/MeshTopologySeed.cs:28` |
| Node positions / links | `SmartX.Api/Data/MeshTopologySeed.cs:51` / `SmartX.Api/Data/MeshTopologySeed.cs:73` |
| Link weight = power cost (distance² / 100) | `SmartX.Api/Data/MeshTopologySeed.cs:115` |
| Graph built from registered devices | `SmartX.Api/Data/MeshTopologySeed.cs:128` |
| BFS from the gateway over live links | `SmartX.Shared/Graphs/MeshReachability.cs:90`, queue loop at `SmartX.Shared/Graphs/MeshReachability.cs:98` |
| Down vs cut off | `SmartX.Shared/Graphs/MeshReachability.cs:124` |
| Where traffic hops instead | `SmartX.Shared/Graphs/MeshReachability.cs:136` |
| BFS source comment | `SmartX.Shared/Graphs/MeshReachability.cs:84` |
| Down nodes come from the Part 2 ActiveAlertSet | `SmartX.Api/Services/MeshTopologyService.cs:134` |
| Prim's algorithm | `SmartX.Shared/Graphs/PrimMinimumSpanningTree.cs:79` |
| PriorityQueue min-heap | `SmartX.Shared/Graphs/PrimMinimumSpanningTree.cs:98` |
| Spanning forest for a disconnected graph | `SmartX.Shared/Graphs/PrimMinimumSpanningTree.cs:101` |
| Total MST cost | `SmartX.Shared/Graphs/PrimMinimumSpanningTree.cs:23` |
| Prim's source comment | `SmartX.Shared/Graphs/PrimMinimumSpanningTree.cs:71` |
| Route = MST path to the gateway | `SmartX.Shared/Graphs/MeshRouter.cs:25` |
| Wiring the algorithms to live state | `SmartX.Api/Services/MeshTopologyService.cs:23` |
| Route by MAC or name | `SmartX.Api/Services/MeshTopologyService.cs:87` |
| Thin endpoints: topology / reachability / MST / route | `SmartX.Api/Endpoints/TopologyEndpoints.cs:16` / `:20` / `:24` / `:28` |
| Recompute and push when a node goes down or recovers | `SmartX.Api/Services/AlertBroadcaster.cs:75`, `SmartX.Api/Services/TopologyBroadcaster.cs:15` |
| Push when a new device joins | `SmartX.Api/Endpoints/SensorEndpoints.cs:75` |
| SignalR event name | `SmartX.Shared/Realtime/HubEvents.cs:19` |
| Healing demo without the simulator (reconnect) | `SmartX.Api/Endpoints/SimulationEndpoints.cs:93`, `SmartX.Shared/Realtime/DisconnectDetector.cs:102` |
| Reachability panel / MST panel | `SmartX.Client/Components/MeshHealthPanel.razor:20` / `SmartX.Client/Components/MstPanel.razor:19` |
| **E. Documentation** | |
| README, implementation report, mapping | this file |
| Feedback log | `FEEDBACK_UPDATES.md` |

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
| Disconnect detector (incl. Part 3 force reconnect) | `DisconnectDetectorTests.cs` |
| Suggestion engine (known history → expected next action) + health analyzer | `SuggestionEngineTests.cs` |
| Search/filter rules, state container (incl. Part 3 topology state and stale snapshot guard) | `DeviceFilterTests.cs`, `LiveStateContainerTests.cs` |
| Part 1 (validator, operators, batch processor, ledger, download URL) | `DeploymentTreeValidatorTests.cs`, `OperatorOverloadTests.cs`, `TelemetryBatchProcessorTests.cs`, `SensorLedgerTests.cs`, `SmartXApiClientTests.cs` |
| **Part 3** AVL tree: in-order ordering, height stays logarithmic over 10,000 sequential inserts, each of the four rotations, range query vs a plain list filter on random data, inclusive boundaries, empty tree, no-result ranges, duplicate timestamps, nodes visited ≪ total, remove-oldest | `AvlTreeTests.cs` |
| **Part 3** per-device trees: per-device isolation, unknown device, cap, parallel writers and readers | `DeviceHistoryTreesTests.cs` |
| **Part 3** graph: add vertex/edge, neighbours, duplicate edge, unknown vertex, invalid input | `MeshGraphTests.cs` |
| **Part 3** BFS: all reachable, down node excluded, cut-off node reported, alternative hop found | `MeshReachabilityTests.cs` |
| **Part 3** Prim's: hand-checked total and edges, V − 1 edges, brute-force comparison on random graphs, spanning forest with unreachable nodes, recompute after a node goes down | `PrimMinimumSpanningTreeTests.cs` |
| **Part 3** route lookup: hop order, costs, total, reroute, unreachable | `MeshRouterTests.cs` |
| **Part 3** history range inputs: the seconds Blazor adds to datetime-local values, half-typed values, commit rules | `HistoryRangeInputTests.cs` |
| **Part 3** history presets: anchoring to the newest stored reading when live data has stopped, the note text | `HistoryPresetWindowTests.cs` |

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
| POST | `/api/simulate/reconnect/{mac}` | **Part 3** - clear a node's disconnected state (healing demo) |
| GET | `/api/history/{device}?from=&to=&page=&pageSize=` | **Part 3** - AVL range query by MAC or name (UTC times, inclusive) |
| GET | `/api/topology` | **Part 3** - nodes, links, BFS reachability and MST in one call |
| GET | `/api/topology/reachability` | **Part 3** - BFS: reachable, down, cut off, reroutes |
| GET | `/api/topology/mst` | **Part 3** - Prim's MST and its total cost |
| GET | `/api/topology/route/{device}` | **Part 3** - a device's MST route, hop by hop |
| WS | `/hubs/telemetry` | SignalR hub: `telemetry`, `alert`, `command`, `queueStats`, `insights`, `topology` |

## Notes

- All gateway state is in memory and resets when the API restarts. The four
  hours of seeded history are regenerated on every start, ending at the
  moment the API started.
- Uploaded files go to `SmartX.Api/UploadedFiles/`, which is git-ignored.
- Sensors registered through the Part 1 form that have no position in
  `MeshTopologySeed` are listed under the map as "not placed in the mesh
  definition". They still work everywhere else.
- `FEEDBACK_UPDATES.md` lists every change made to earlier parts while
  building the next one, and the response to lecturer feedback.
