using SmartX.Shared.Realtime;

namespace SmartX.Api.Services;

/// <summary>
/// Finds a device by MAC address first (O(1) dictionary hit), then by exact
/// display name, so routes like /api/history/{device} accept either
/// "24:6F:28:A1:00:03" or "Moisture Sensor C".
/// </summary>
public static class DeviceLookup
{
    public static LiveDevice? Resolve(DeviceDirectory directory, string macOrName)
    {
        if (string.IsNullOrWhiteSpace(macOrName))
        {
            return null;
        }

        var term = macOrName.Trim();
        if (directory.TryGet(term, out var byMac) && byMac is not null)
        {
            return byMac;
        }

        return directory.Snapshot().FirstOrDefault(d => string.Equals(d.DisplayName, term, StringComparison.OrdinalIgnoreCase));
    }
}
