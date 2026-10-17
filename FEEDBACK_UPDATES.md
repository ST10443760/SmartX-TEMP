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

## Part 2 changes

This section will record changes made to Part 2 during the final PoE.
