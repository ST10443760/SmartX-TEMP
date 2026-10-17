# Feedback Updates

This file tracks changes made to earlier parts of the Smart-X POE, either in
response to lecturer feedback or because a problem was found while building
the next part. Each entry says what was wrong, what changed, and where.

## Part 1 changes made during Part 2

| # | What was wrong | What changed | File(s) |
|---|---|---|---|
| 1 | `SensorLedger` was a plain `List<T>` with no locking. That was fine while only the Blazor form registered sensors, but in Part 2 the simulator registers devices while the background worker and endpoints read the ledger at the same time, which can throw "collection was modified" errors. | Every member now takes a private lock, and enumeration walks a snapshot copy. Existing tests still pass unchanged. | `SmartX.Shared/Collections/SensorLedger.cs` |
| 2 | Sensors could only be identified by MAC address, so the Part 2 brief's "search by name" had nothing to search. | Added an optional `DisplayName` (and `ActuatorKind` for actuators) to the registration request and the sensor record. The seed data now has friendly names. | `SmartX.Shared/Models/SensorRecord.cs`, `SmartX.Shared/Dtos/SensorRegistrationRequest.cs`, `SmartX.Api/Data/SensorSeed.cs` |

## Lecturer feedback on Part 1

No written feedback had been received when Part 2 was built. Entries will be
added here when it arrives.
