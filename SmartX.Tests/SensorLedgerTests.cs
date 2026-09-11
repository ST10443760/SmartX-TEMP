using SmartX.Shared.Collections;
using SmartX.Shared.Enums;
using SmartX.Shared.Models;

namespace SmartX.Tests;

public class SensorLedgerTests
{
    private static SensorRecord MakeSensor(string mac, SensorCategory category = SensorCategory.Environmental)
        => new()
        {
            Id = Guid.NewGuid(),
            MacAddress = mac,
            DeploymentPath = DeploymentPath.Of("Facility A", "Zone 1"),
            Category = category
        };

    [Fact]
    public void Register_NewSensor_IsRetrievableByMacAddress()
    {
        var ledger = new SensorLedger();
        var sensor = MakeSensor("AA:BB:CC:00:00:01");

        ledger.Register(sensor);

        Assert.Equal(1, ledger.Count);
        Assert.Same(sensor, ledger.FindByMacAddress("aa:bb:cc:00:00:01"));
    }

    [Fact]
    public void Register_DuplicateMacAddress_Throws()
    {
        var ledger = new SensorLedger();
        ledger.Register(MakeSensor("AA:BB:CC:00:00:01"));

        Assert.Throws<InvalidOperationException>(() => ledger.Register(MakeSensor("AA:BB:CC:00:00:01")));
    }

    [Fact]
    public void InCategory_FiltersByCategory()
    {
        var ledger = new SensorLedger();
        ledger.Register(MakeSensor("AA:00", SensorCategory.Environmental));
        ledger.Register(MakeSensor("AA:01", SensorCategory.PowerConsumption));
        ledger.Register(MakeSensor("AA:02", SensorCategory.Environmental));

        var environmental = ledger.InCategory(SensorCategory.Environmental).ToList();

        Assert.Equal(2, environmental.Count);
    }

    [Fact]
    public void TryAttachFile_ExistingSensor_AddsAttachmentAndReturnsTrue()
    {
        var ledger = new SensorLedger();
        ledger.Register(MakeSensor("AA:BB:CC:00:00:01"));

        var attached = ledger.TryAttachFile("AA:BB:CC:00:00:01", new SensorAttachment
        {
            Id = Guid.NewGuid(),
            SensorMacAddress = "AA:BB:CC:00:00:01",
            FileName = "config.json",
            ContentType = "application/json",
            SizeBytes = 128,
            StoredFileName = "guid_config.json"
        });

        Assert.True(attached);
        Assert.Single(ledger.FindByMacAddress("AA:BB:CC:00:00:01")!.Attachments);
    }

    [Fact]
    public void TryAttachFile_UnknownSensor_ReturnsFalse()
    {
        var ledger = new SensorLedger();

        var attached = ledger.TryAttachFile("00:00:00:00:00:00", new SensorAttachment
        {
            Id = Guid.NewGuid(),
            SensorMacAddress = "00:00:00:00:00:00",
            FileName = "log.txt",
            ContentType = "text/plain",
            SizeBytes = 10,
            StoredFileName = "guid_log.txt"
        });

        Assert.False(attached);
    }
}
