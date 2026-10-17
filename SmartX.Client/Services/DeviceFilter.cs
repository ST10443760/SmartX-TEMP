using SmartX.Shared.Dtos;
using SmartX.Shared.Enums;

namespace SmartX.Client.Services;

/// <summary>
/// The command stream's search and filter rules in one pure, testable place:
/// free-text search over MAC address and display name, an operational
/// category filter, and an "active alerts only" toggle.
/// </summary>
public static class DeviceFilter
{
    public static bool Matches(string mac, string name, SensorCategory category, bool hasAlert, string? search, SensorCategory? categoryFilter, bool alertsOnly)
    {
        if (categoryFilter is { } wanted && category != wanted)
        {
            return false;
        }

        if (alertsOnly && !hasAlert)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        var term = search.Trim();
        return mac.Contains(term, StringComparison.OrdinalIgnoreCase)
            || name.Contains(term, StringComparison.OrdinalIgnoreCase);
    }

    public static IEnumerable<DeviceStatus> Apply(LiveStateContainer state)
        => state.Devices
            .Where(d => Matches(d.MacAddress, d.DisplayName, d.Category, state.HasAlert(d.MacAddress), state.SearchText, state.CategoryFilter, state.AlertsOnly))
            .OrderByDescending(d => state.HasAlert(d.MacAddress))
            .ThenBy(d => d.Category)
            .ThenBy(d => d.DisplayName);

    public static IEnumerable<LiveReading> ApplyToFeed(LiveStateContainer state)
        => state.Feed.Where(r => Matches(r.MacAddress, r.DeviceName, r.Category, state.HasAlert(r.MacAddress) || r.Severity != AlertSeverity.Normal, state.SearchText, state.CategoryFilter, state.AlertsOnly));
}
