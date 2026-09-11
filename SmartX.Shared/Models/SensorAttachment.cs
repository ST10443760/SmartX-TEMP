namespace SmartX.Shared.Models;

/// <summary>
/// Metadata for a config file, deployment photo, or hardware log uploaded
/// against a sensor profile. The bytes live on disk under the API's upload
/// folder; this record is what the client and API pass around.
/// </summary>
public sealed class SensorAttachment
{
    public required Guid Id { get; init; }

    public required string SensorMacAddress { get; init; }

    public required string FileName { get; init; }

    public required string ContentType { get; init; }

    public required long SizeBytes { get; init; }

    public DateTimeOffset UploadedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Name the file is actually stored under on disk (collision-safe).</summary>
    public required string StoredFileName { get; init; }
}
