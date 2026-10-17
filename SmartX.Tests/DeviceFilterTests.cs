using SmartX.Client.Services;
using SmartX.Shared.Enums;

namespace SmartX.Tests;

public class DeviceFilterTests
{
    [Theory]
    [InlineData("moisture sensor b", true)]
    [InlineData("A1:00:02", true)]
    [InlineData("Pump", false)]
    [InlineData("", true)]
    public void Search_MatchesNameOrMacCaseInsensitively(string search, bool expected)
        => Assert.Equal(expected, DeviceFilter.Matches("24:6F:28:A1:00:02", "Moisture Sensor B", SensorCategory.Environmental, false, search, null, false));

    [Fact]
    public void CategoryFilter_ExcludesOtherCategories()
    {
        Assert.False(DeviceFilter.Matches("AA", "Pump A", SensorCategory.Actuator, false, null, SensorCategory.Environmental, false));
        Assert.True(DeviceFilter.Matches("AA", "Pump A", SensorCategory.Actuator, false, null, SensorCategory.Actuator, false));
    }

    [Fact]
    public void AlertsOnly_KeepsOnlyDevicesWithAnActiveAlert()
    {
        Assert.False(DeviceFilter.Matches("AA", "Power Meter A", SensorCategory.PowerConsumption, false, null, null, true));
        Assert.True(DeviceFilter.Matches("AA", "Power Meter A", SensorCategory.PowerConsumption, true, null, null, true));
    }
}
