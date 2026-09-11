using System.Collections.Concurrent;
using SmartX.Shared.Collections;
using SmartX.Shared.Dtos;
using SmartX.Shared.Enums;

namespace SmartX.Api.Services;

/// <summary>
/// Backs the "Mesh Pulse" dashboard engagement feature: continuously walks
/// every registered sensor, nudges a simulated reading with a random walk,
/// and occasionally injects an anomaly or a dropped-node event so the
/// dashboard always has something changing to react to. The client polls
/// <see cref="Snapshot"/> through a Minimal API endpoint every couple of
/// seconds rather than the gateway pushing over a socket (SignalR arrives in
/// Part 2), but the state itself is genuinely live, not a canned animation.
/// </summary>
public sealed class TelemetryPulseGenerator(SensorLedger ledger) : BackgroundService
{
    private readonly ConcurrentDictionary<string, TelemetryPulse> _pulses = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, double> _lastValue = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<TelemetryPulse> Snapshot()
        => _pulses.Values.OrderByDescending(p => p.AnomalyScore).ToList();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var rng = Random.Shared;

        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var sensor in ledger)
            {
                var (unit, baseline) = sensor.Category switch
                {
                    SensorCategory.Environmental => ("%", 45.0),
                    SensorCategory.PowerConsumption => ("W", 220.0),
                    SensorCategory.Actuator => ("state", 1.0),
                    _ => ("", 0.0)
                };

                var previous = _lastValue.GetOrAdd(sensor.MacAddress, baseline);
                var walked = previous + ((rng.NextDouble() - 0.5) * (baseline * 0.08));

                // Occasionally inject a spike so the anomaly-highlighting logic
                // has something real to catch during a demo.
                var isSpike = rng.NextDouble() < 0.06;
                var value = isSpike ? walked + (baseline * (rng.NextDouble() > 0.5 ? 0.6 : -0.6)) : walked;
                value = Math.Max(0, Math.Round(value, 1));
                _lastValue[sensor.MacAddress] = value;

                var deviationRatio = baseline == 0 ? 0 : Math.Abs(value - baseline) / baseline;
                var anomalyScore = (int)Math.Clamp(deviationRatio * 100, 0, 100);

                var quality = anomalyScore switch
                {
                    >= 60 => TelemetryQuality.Critical,
                    >= 30 => TelemetryQuality.Warning,
                    _ => TelemetryQuality.Normal
                };

                _pulses[sensor.MacAddress] = new TelemetryPulse
                {
                    SensorId = sensor.MacAddress,
                    Category = sensor.Category,
                    Value = value,
                    Unit = unit,
                    Quality = quality,
                    TimestampUtc = DateTimeOffset.UtcNow,
                    AnomalyScore = anomalyScore
                };
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // Expected on shutdown.
            }
        }
    }
}
