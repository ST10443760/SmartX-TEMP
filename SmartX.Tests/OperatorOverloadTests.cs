using SmartX.Shared.Models;

namespace SmartX.Tests;

public class OperatorOverloadTests
{
    [Fact]
    public void PowerReading_Plus_AggregatesWattsAcrossMeters()
    {
        var meter1 = new PowerReading("Meter-01", 120.5);
        var meter2 = new PowerReading("Meter-02", 79.5);

        var circuitLoad = meter1 + meter2;

        Assert.Equal(200.0, circuitLoad.Watts, precision: 3);
        Assert.Equal("Meter-01+Meter-02", circuitLoad.SensorId);
    }

    [Fact]
    public void PowerReading_Minus_ReturnsDelta()
    {
        var before = new PowerReading("Meter-01", 300);
        var after = new PowerReading("Meter-01", 250);

        var delta = before - after;

        Assert.Equal(50, delta.Watts, precision: 3);
    }

    [Fact]
    public void PowerReading_ComparisonOperators_CompareWattage()
    {
        var high = new PowerReading("Meter-01", 500);
        var mid = new PowerReading("Meter-03", 300);
        var low = new PowerReading("Meter-02", 100);

        Assert.True(high > low);
        Assert.True(low < high);
        Assert.True(high >= mid);
        Assert.True(low <= mid);
    }

    [Fact]
    public void PowerReading_Equality_ComparesSensorIdAndWatts()
    {
        var a = new PowerReading("Meter-01", 42);
        var b = new PowerReading("Meter-01", 42);
        var c = new PowerReading("Meter-01", 43);

        Assert.True(a == b);
        Assert.True(a != c);
    }

    [Fact]
    public void MoistureReading_Minus_ReturnsSignedDrift()
    {
        var earlier = new MoistureReading("Node-B", 45);
        var later = new MoistureReading("Node-B", 18);

        double drop = later - earlier;

        Assert.Equal(-27, drop, precision: 3);
    }

    [Fact]
    public void MoistureReading_ComparisonOperators_TriggerLowThresholdLogic()
    {
        var reading = new MoistureReading("Node-B", 18);
        var threshold = new MoistureReading("Threshold", 20);

        Assert.True(reading < threshold);
    }
}
