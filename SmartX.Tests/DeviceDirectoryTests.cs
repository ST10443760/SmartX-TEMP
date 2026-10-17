using SmartX.Shared.Enums;
using SmartX.Shared.Realtime;

namespace SmartX.Tests;

public class DeviceDirectoryTests
{
    [Fact]
    public void TryGet_KnownMac_HitsRegardlessOfCase()
    {
        var directory = new DeviceDirectory();
        directory.Register("AA:BB:CC:00:00:01", "Moisture Sensor B", SensorCategory.Environmental);

        var found = directory.TryGet("aa:bb:cc:00:00:01", out var device);

        Assert.True(found);
        Assert.Equal("Moisture Sensor B", device!.DisplayName);
    }

    [Fact]
    public void TryGet_UnknownMac_MissesWithoutThrowing()
    {
        var directory = new DeviceDirectory();
        directory.Register("AA:BB:CC:00:00:01", "Moisture Sensor B", SensorCategory.Environmental);

        var found = directory.TryGet("FF:FF:FF:FF:FF:FF", out var device);

        Assert.False(found);
        Assert.Null(device);
    }

    [Fact]
    public void RecordReading_UnknownMac_ReturnsFalse()
    {
        var directory = new DeviceDirectory();

        Assert.False(directory.RecordReading("FF:FF", 10, "%", DateTime.UtcNow, AlertSeverity.Normal));
    }

    [Fact]
    public void RecordReading_KnownMac_UpdatesLatestValue()
    {
        var directory = new DeviceDirectory();
        directory.Register("AA:01", "Power Meter A", SensorCategory.PowerConsumption);
        var at = new DateTime(2026, 10, 17, 8, 0, 0, DateTimeKind.Utc);

        directory.RecordReading("AA:01", 640, "W", at, AlertSeverity.Normal);

        directory.TryGet("AA:01", out var device);
        Assert.Equal(640, device!.LastValue);
        Assert.Equal(at, device.LastSeenUtc);
    }

    [Fact]
    public void Register_DuplicateMac_KeepsTheOriginalEntry()
    {
        var directory = new DeviceDirectory();
        directory.Register("AA:01", "Original", SensorCategory.Environmental);

        var added = directory.Register("aa:01", "Impostor", SensorCategory.Actuator);

        Assert.False(added);
        Assert.Equal(1, directory.Count);
        directory.TryGet("AA:01", out var device);
        Assert.Equal("Original", device!.DisplayName);
    }
}
