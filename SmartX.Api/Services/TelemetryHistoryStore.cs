using System.Collections.Concurrent;
using SmartX.Shared.Models;
using SmartX.Shared.Processing;

namespace SmartX.Api.Services;

/// <summary>
/// Holds each sensor's historical readings as an optimised
/// <see cref="List{T}"/>, populated at startup from jagged raw batches via
/// <see cref="TelemetryBatchProcessor"/> - the array-to-List transformation
/// the ingestion pipeline requirement asks for, done once against seeded
/// data rather than buried inline in a page.
/// </summary>
public sealed class TelemetryHistoryStore
{
    private readonly ConcurrentDictionary<string, List<TelemetryPacket<double>>> _history = new(StringComparer.OrdinalIgnoreCase);

    public void SeedFromJaggedBatches(string sensorId, string unit, int burstCount, int minPerBurst, int maxPerBurst, double baseline, double noise)
    {
        var rng = Random.Shared;

        // A jagged array: every burst can capture a different number of
        // samples, mirroring real polling where a burst is sometimes cut
        // short. A rectangular double[,] could not model that.
        var rawBatches = new double[burstCount][];
        var current = baseline;

        for (var b = 0; b < burstCount; b++)
        {
            var samplesInBurst = rng.Next(minPerBurst, maxPerBurst + 1);
            rawBatches[b] = new double[samplesInBurst];

            for (var i = 0; i < samplesInBurst; i++)
            {
                current += (rng.NextDouble() - 0.5) * noise;
                rawBatches[b][i] = Math.Round(current, 2);
            }
        }

        var flattened = TelemetryBatchProcessor.FlattenBatches(
            rawBatches,
            sensorId,
            unit,
            DateTimeOffset.UtcNow.AddMinutes(-burstCount),
            TimeSpan.FromMinutes(1));

        _history[sensorId] = flattened;
    }

    public IReadOnlyList<TelemetryPacket<double>> GetHistory(string sensorId, int take = 50)
        => _history.TryGetValue(sensorId, out var list)
            ? list.Skip(Math.Max(0, list.Count - take)).ToList()
            : [];

    public IReadOnlyCollection<List<TelemetryPacket<double>>> AllHistories() => _history.Values;

    public IReadOnlyCollection<string> SeededSensorIds => _history.Keys.ToList();
}
