using SmartX.Shared.Enums;
using SmartX.Shared.Models;

namespace SmartX.Shared.Processing;

/// <summary>
/// Handles the raw, sequential batches a gateway receives while a sensor is
/// polled in short bursts - each burst is a row that can hold a different
/// number of readings than the next, which is exactly what a jagged array
/// (rather than a rectangular 2D array) models. Historical batches accumulate
/// here as they arrive, then get flattened into a single optimised
/// <see cref="List{T}"/> of <see cref="TelemetryPacket{T}"/> for everything
/// downstream (storage, dashboards, the anomaly engine) to work with.
/// </summary>
public static class TelemetryBatchProcessor
{
    /// <summary>
    /// Flattens jagged, per-burst raw readings into a single chronological
    /// List&lt;TelemetryPacket&lt;double&gt;&gt;, stamping each reading with a
    /// timestamp derived from its batch/offset position.
    /// </summary>
    /// <param name="rawBatches">
    /// rawBatches[b] is the array of readings captured during burst b; bursts
    /// do not need to be the same length (a jagged array, not a rectangular
    /// double[,]), which mirrors real polling where a burst can be cut short
    /// by a dropped packet.
    /// </param>
    public static List<TelemetryPacket<double>> FlattenBatches(
        double[][] rawBatches,
        string sensorId,
        string unit,
        DateTimeOffset batchStartUtc,
        TimeSpan sampleInterval)
    {
        ArgumentNullException.ThrowIfNull(rawBatches);

        var flattened = new List<TelemetryPacket<double>>();
        var cursor = batchStartUtc;

        for (var batchIndex = 0; batchIndex < rawBatches.Length; batchIndex++)
        {
            var burst = rawBatches[batchIndex];
            for (var i = 0; i < burst.Length; i++)
            {
                flattened.Add(new TelemetryPacket<double>
                {
                    SensorId = sensorId,
                    Unit = unit,
                    Value = burst[i],
                    TimestampUtc = cursor,
                    Quality = TelemetryQuality.Normal
                });
                cursor += sampleInterval;
            }
        }

        return flattened;
    }

    /// <summary>
    /// Builds a fixed-width snapshot grid (a true multi-dimensional array,
    /// rows = sensors, columns = the last <paramref name="windowSize"/>
    /// readings) used to render a quick "last N readings per node" table
    /// without allocating a jagged structure when every row is known to be
    /// the same length.
    /// </summary>
    public static double[,] BuildLatestReadingsGrid(IReadOnlyList<List<TelemetryPacket<double>>> perSensorHistory, int windowSize)
    {
        var grid = new double[perSensorHistory.Count, windowSize];

        for (var row = 0; row < perSensorHistory.Count; row++)
        {
            var history = perSensorHistory[row];

            // Right-align the window: the last column is always the most
            // recent reading. If there isn't enough history yet, the leading
            // columns are left as NaN rather than shifting the data around.
            var offset = history.Count - windowSize;

            for (var col = 0; col < windowSize; col++)
            {
                var sourceIndex = offset + col;
                grid[row, col] = sourceIndex >= 0 && sourceIndex < history.Count
                    ? history[sourceIndex].Value
                    : double.NaN;
            }
        }

        return grid;
    }
}
